using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Domain.Filler;
using ErsatzTV.Core.Interfaces.Scheduling;
using ErsatzTV.Core.Scheduling.YamlScheduling.Models;
using Microsoft.Extensions.Logging;
using TimeSpanParserUtil;

namespace ErsatzTV.Core.Scheduling.YamlScheduling.Handlers;

public class YamlPlayoutDurationHandler(EnumeratorCache enumeratorCache) : YamlPlayoutContentHandler(enumeratorCache)
{
    public override async Task<bool> Handle(
        YamlPlayoutContext context,
        YamlPlayoutInstruction instruction,
        ILogger<YamlPlayoutBuilder> logger,
        CancellationToken cancellationToken)
    {
        if (instruction is not YamlPlayoutDurationInstruction duration)
        {
            return false;
        }

        // TODO: move to up-front validation somewhere
        if (!TimeSpanParser.TryParse(duration.Duration, out TimeSpan timeSpan))
        {
            return false;
        }

        DateTimeOffset targetTime = context.CurrentTime.Add(timeSpan);

        Option<IMediaCollectionEnumerator> maybeEnumerator = await GetContentEnumerator(
            context,
            instruction.Content,
            logger,
            cancellationToken);

        Option<IMediaCollectionEnumerator> fallbackEnumerator = await GetContentEnumerator(
            context,
            duration.Fallback,
            logger,
            cancellationToken);

        foreach (IMediaCollectionEnumerator enumerator in maybeEnumerator)
        {
            context.CurrentTime = Schedule(
                context,
                targetTime,
                duration.DiscardAttempts,
                duration.Trim,
                GetFillerKind(duration),
                enumerator,
                fallbackEnumerator);

            return true;
        }

        return false;
    }

    protected static DateTimeOffset Schedule(
        YamlPlayoutContext context,
        DateTimeOffset targetTime,
        int discardAttempts,
        bool trim,
        FillerKind fillerKind,
        IMediaCollectionEnumerator enumerator,
        Option<IMediaCollectionEnumerator> fallbackEnumerator)
    {
        bool done = false;
        TimeSpan remainingToFill = targetTime - context.CurrentTime;
        while (!done && enumerator.Current.IsSome && remainingToFill > TimeSpan.Zero)
        {
            foreach (MediaItem mediaItem in enumerator.Current)
            {
                TimeSpan itemDuration = DurationForMediaItem(mediaItem);

                var playoutItem = new PlayoutItem
                {
                    MediaItemId = mediaItem.Id,
                    Start = context.CurrentTime.UtcDateTime,
                    Finish = context.CurrentTime.UtcDateTime + itemDuration,
                    InPoint = TimeSpan.Zero,
                    OutPoint = itemDuration,
                    GuideGroup = context.GuideGroup,
                    FillerKind = fillerKind
                    //DisableWatermarks = !allowWatermarks
                };

                if (remainingToFill - itemDuration >= TimeSpan.Zero)
                {
                    remainingToFill -= itemDuration;
                    context.CurrentTime += itemDuration;

                    context.Playout.Items.Add(playoutItem);
                    enumerator.MoveNext();
                }
                else if (discardAttempts > 0)
                {
                    // item won't fit; try the next one
                    discardAttempts--;
                    enumerator.MoveNext();
                }
                else if (trim)
                {
                    // trim item to exactly fit
                    remainingToFill = TimeSpan.Zero;
                    context.CurrentTime = targetTime;

                    playoutItem.Finish = targetTime.UtcDateTime;
                    playoutItem.OutPoint = playoutItem.Finish - playoutItem.Start;

                    context.Playout.Items.Add(playoutItem);
                    enumerator.MoveNext();
                }
                else if (fallbackEnumerator.IsSome)
                {
                    foreach (IMediaCollectionEnumerator fallback in fallbackEnumerator)
                    {
                        remainingToFill = TimeSpan.Zero;
                        done = true;

                        // replace with fallback content
                        foreach (MediaItem fallbackItem in fallback.Current)
                        {
                            playoutItem.MediaItemId = fallbackItem.Id;
                            playoutItem.Finish = targetTime.UtcDateTime;
                            playoutItem.FillerKind = FillerKind.Fallback;

                            context.Playout.Items.Add(playoutItem);
                            fallback.MoveNext();
                        }
                    }
                }
                else
                {
                    // item won't fit; we're done
                    done = true;
                }
            }
        }

        return targetTime;
    }
}