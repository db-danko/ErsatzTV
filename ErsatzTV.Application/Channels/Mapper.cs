﻿using ErsatzTV.Core.Api.Channels;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Domain.Scheduling;

namespace ErsatzTV.Application.Channels;

internal static class Mapper
{
    internal static ChannelViewModel ProjectToViewModel(Channel channel) =>
        new(
            channel.Id,
            channel.Number,
            channel.Name,
            channel.Group,
            channel.Categories,
            channel.FFmpegProfileId,
            GetLogo(channel),
            channel.PreferredAudioLanguageCode,
            channel.PreferredAudioTitle,
            channel.StreamingMode,
            channel.WatermarkId,
            channel.FallbackFillerId,
            channel.Playouts?.Count ?? 0,
            channel.PreferredSubtitleLanguageCode,
            channel.SubtitleMode,
            channel.MusicVideoCreditsMode,
            channel.MusicVideoCreditsTemplate);

    internal static ChannelResponseModel ProjectToResponseModel(Channel channel) =>
        new(
            channel.Id,
            channel.Number,
            channel.Name,
            channel.FFmpegProfileId,
            channel.FFmpegProfile.Name,
            channel.PreferredAudioLanguageCode,
            GetStreamingMode(channel),
            channel.ChannelScheduleDayTemplates.Map(ProjectToResponseModel).ToList());

    internal static ChannelScheduleDayTemplateResponseModel ProjectToResponseModel(
        ChannelScheduleDayTemplate channelScheduleDayTemplate) =>
        new(
            channelScheduleDayTemplate.Index,
            channelScheduleDayTemplate.ScheduleDayTemplate.Name,
            channelScheduleDayTemplate.DaysOfWeek,
            channelScheduleDayTemplate.DaysOfMonth,
            channelScheduleDayTemplate.MonthsOfYear);

    private static string GetLogo(Channel channel) =>
        Optional(channel.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Logo))
            .Match(a => a.Path, string.Empty);

    private static string GetStreamingMode(Channel channel) =>
        channel.StreamingMode switch
        {
            StreamingMode.TransportStream => "MPEG-TS (Legacy)",
            StreamingMode.TransportStreamHybrid => "MPEG-TS",
            StreamingMode.HttpLiveStreamingDirect => "HLS Direct",
            StreamingMode.HttpLiveStreamingSegmenter => "HLS Segmenter",
            _ => throw new ArgumentOutOfRangeException()
        };
}
