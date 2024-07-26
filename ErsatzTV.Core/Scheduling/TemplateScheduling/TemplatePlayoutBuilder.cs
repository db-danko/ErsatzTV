using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Metadata;
using ErsatzTV.Core.Interfaces.Repositories;
using ErsatzTV.Core.Interfaces.Scheduling;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ErsatzTV.Core.Scheduling.TemplateScheduling;

public class TemplatePlayoutBuilder(
    ILocalFileSystem localFileSystem,
    IConfigElementRepository configElementRepository,
    IMediaCollectionRepository mediaCollectionRepository,
    ILogger<TemplatePlayoutBuilder> logger)
    : ITemplatePlayoutBuilder
{
    public async Task<Playout> Build(Playout playout, PlayoutBuildMode mode, CancellationToken cancellationToken)
    {
        if (!localFileSystem.FileExists(playout.TemplateFile))
        {
            logger.LogWarning("Playout template file {File} does not exist; aborting.", playout.TemplateFile);
            return playout;
        }

        PlayoutTemplate playoutTemplate = await LoadTemplate(playout, cancellationToken);

        DateTimeOffset start = DateTimeOffset.Now;
        int daysToBuild = await GetDaysToBuild();
        DateTimeOffset finish = start.AddDays(daysToBuild);

        if (mode is not PlayoutBuildMode.Reset)
        {
            logger.LogWarning("Template playouts can only be reset; ignoring build mode {Mode}", mode.ToString());
            return playout;
        }

        // these are only for reset
        playout.Seed = new Random().Next();
        playout.Items.Clear();

        DateTimeOffset currentTime = start;

        // load content and content enumerators on demand
        Dictionary<string, IMediaCollectionEnumerator> enumerators = new();
        System.Collections.Generic.HashSet<string> missingContentKeys = [];

        int itemsAfterRepeat = playout.Items.Count;
        var index = 0;
        while (currentTime < finish)
        {
            if (index >= playoutTemplate.Playout.Count)
            {
                logger.LogInformation("Reached the end of the playout template; stopping");
                break;
            }

            PlayoutTemplateItem playoutItem = playoutTemplate.Playout[index];

            // repeat resets index into template playout
            if (playoutItem is PlayoutTemplateRepeatItem)
            {
                index = 0;
                if (playout.Items.Count == itemsAfterRepeat)
                {
                    logger.LogWarning("Repeat encountered without adding any playout items; aborting");
                    break;
                }

                itemsAfterRepeat = playout.Items.Count;
                continue;
            }

            Option<IMediaCollectionEnumerator> maybeEnumerator = await GetCachedEnumeratorForContent(
                playout,
                playoutTemplate,
                enumerators,
                playoutItem.Content,
                cancellationToken);

            if (maybeEnumerator.IsNone)
            {
                if (!missingContentKeys.Contains(playoutItem.Content))
                {
                    logger.LogWarning("Unable to locate content with key {Key}", playoutItem.Content);
                    missingContentKeys.Add(playoutItem.Content);
                }
            }

            foreach (IMediaCollectionEnumerator enumerator in maybeEnumerator)
            {
                switch (playoutItem)
                {
                    case PlayoutTemplateCountItem count:
                        currentTime = PlayoutTemplateSchedulerCount.Schedule(playout, currentTime, count, enumerator);
                        break;
                    case PlayoutTemplateDurationItem duration:
                        Option<IMediaCollectionEnumerator> durationFallbackEnumerator = await GetCachedEnumeratorForContent(
                            playout,
                            playoutTemplate,
                            enumerators,
                            duration.Fallback,
                            cancellationToken);
                        currentTime = PlayoutTemplateSchedulerDuration.Schedule(
                            playout,
                            currentTime,
                            duration,
                            enumerator,
                            durationFallbackEnumerator);
                        break;
                    case PlayoutTemplatePadToNextItem padToNext:
                        Option<IMediaCollectionEnumerator> fallbackEnumerator = await GetCachedEnumeratorForContent(
                            playout,
                            playoutTemplate,
                            enumerators,
                            padToNext.Fallback,
                            cancellationToken);
                        currentTime = PlayoutTemplateSchedulerPadToNext.Schedule(
                            playout,
                            currentTime,
                            padToNext,
                            enumerator,
                            fallbackEnumerator);
                        break;
                }
            }

            index++;
        }

        return playout;
    }

    private async Task<int> GetDaysToBuild() =>
        await configElementRepository
            .GetValue<int>(ConfigElementKey.PlayoutDaysToBuild)
            .IfNoneAsync(2);

    private async Task<Option<IMediaCollectionEnumerator>> GetCachedEnumeratorForContent(
        Playout playout,
        PlayoutTemplate playoutTemplate,
        Dictionary<string, IMediaCollectionEnumerator> enumerators,
        string contentKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contentKey))
        {
            return Option<IMediaCollectionEnumerator>.None;
        }

        if (!enumerators.TryGetValue(contentKey, out IMediaCollectionEnumerator enumerator))
        {
            Option<IMediaCollectionEnumerator> maybeEnumerator =
                await GetEnumeratorForContent(playout, contentKey, playoutTemplate, cancellationToken);

            if (maybeEnumerator.IsNone)
            {
                return Option<IMediaCollectionEnumerator>.None;
            }

            foreach (IMediaCollectionEnumerator e in maybeEnumerator)
            {
                enumerator = maybeEnumerator.ValueUnsafe();
                enumerators.Add(contentKey, enumerator);
            }
        }

        return Some(enumerator);
    }

    private async Task<Option<IMediaCollectionEnumerator>> GetEnumeratorForContent(
        Playout playout,
        string contentKey,
        PlayoutTemplate playoutTemplate,
        CancellationToken cancellationToken)
    {
        int index = playoutTemplate.Content.FindIndex(c => c.Key == contentKey);
        if (index < 0)
        {
            return Option<IMediaCollectionEnumerator>.None;
        }

        List<MediaItem> items = [];

        PlayoutTemplateContentItem content = playoutTemplate.Content[index];
        switch (content)
        {
            case PlayoutTemplateContentSearchItem search:
                items = await mediaCollectionRepository.GetSmartCollectionItems(search.Query);
                break;
            case PlayoutTemplateContentShowItem show:
                items = await mediaCollectionRepository.GetShowItemsByShowGuids(
                    show.Guids.Map(g => $"{g.Source}://{g.Value}").ToList());
                break;
        }

        var state = new CollectionEnumeratorState { Seed = playout.Seed + index, Index = 0 };
        switch (Enum.Parse<PlaybackOrder>(content.Order, true))
        {
            case PlaybackOrder.Chronological:
                return new ChronologicalMediaCollectionEnumerator(items, state);
            case PlaybackOrder.Shuffle:
                // TODO: fix this
                var groupedMediaItems = items.Map(mi => new GroupedMediaItem(mi, null)).ToList();
                return new ShuffledMediaCollectionEnumerator(groupedMediaItems, state, cancellationToken);
        }

        return Option<IMediaCollectionEnumerator>.None;
    }

    private static async Task<PlayoutTemplate> LoadTemplate(Playout playout, CancellationToken cancellationToken)
    {
        string yaml = await File.ReadAllTextAsync(playout.TemplateFile, cancellationToken);

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeDiscriminatingNodeDeserializer(
                o =>
                {
                    var contentKeyMappings = new Dictionary<string, Type>
                    {
                        { "search", typeof(PlayoutTemplateContentSearchItem) },
                        { "show", typeof(PlayoutTemplateContentShowItem) }
                    };

                    o.AddUniqueKeyTypeDiscriminator<PlayoutTemplateContentItem>(contentKeyMappings);

                    var instructionKeyMappings = new Dictionary<string, Type>
                    {
                        { "count", typeof(PlayoutTemplateCountItem) },
                        { "duration", typeof(PlayoutTemplateDurationItem) },
                        { "pad_to_next", typeof(PlayoutTemplatePadToNextItem) },
                        { "repeat", typeof(PlayoutTemplateRepeatItem) }
                    };

                    o.AddUniqueKeyTypeDiscriminator<PlayoutTemplateItem>(instructionKeyMappings);
                })
            .Build();

        return deserializer.Deserialize<PlayoutTemplate>(yaml);
    }
}