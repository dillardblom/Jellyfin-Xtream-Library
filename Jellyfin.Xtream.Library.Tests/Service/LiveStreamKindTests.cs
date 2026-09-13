// Copyright (C) 2024  Roland Breitschaft

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #112. stream_type was parsed into the models and then never read, so radio stations were
// published as television everywhere. The risk in fixing it is over-reach: stream_type is also
// where providers keep their own bookkeeping, so anything not confirmed to mean radio has to stay
// television.

using System.Linq;
using FluentAssertions;
using Jellyfin.Xtream.Library.Client.Models;
using Jellyfin.Xtream.Library.Service;
using Jellyfin.Xtream.Library.Service.Models;
using Xunit;

namespace Jellyfin.Xtream.Library.Tests.Service;

public class LiveStreamKindTests
{
    [Theory]
    [InlineData("radio_streams")]
    [InlineData("RADIO_STREAMS")]
    [InlineData("  radio_streams  ")]
    public void TheConfirmedRadioValueIsRecognised(string streamType)
    {
        LiveStreamKind.IsRadio(streamType).Should().BeTrue();
    }

    [Theory]
    [InlineData("live")]
    [InlineData("created_live")]     // seen in the wild across Kids, Movies and Sports
    [InlineData("radio")]            // close, but not the value any provider actually sends
    [InlineData("radio_stream")]
    [InlineData("movie")]
    [InlineData("series")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnythingElseStaysTelevision(string? streamType)
    {
        // A mislabelled television channel is harder to find than a radio station in the TV list,
        // so the guess has to fall this way.
        LiveStreamKind.IsRadio(streamType).Should().BeFalse();
    }
}

public class RadioChannelSnapshotTests
{
    private static LiveStreamInfo Channel(int id, string streamType) => new()
    {
        StreamId = id,
        Name = $"Channel {id}",
        StreamType = streamType,
        ProviderIndex = 0,
    };

    [Fact]
    public void StreamTypeSurvivesTheSnapshotRoundTrip()
    {
        // The M3U is often rendered from the snapshot rather than from a fresh provider fetch.
        // Before this was stored, a station came out as radio on the runs that hit the provider
        // and as television on the runs served from disk, which is the worst kind of bug to chase.
        var snapshot = LiveChannelSnapshot.FromChannels(
            [Channel(1, "radio_streams"), Channel(2, "live")],
            new System.Collections.Generic.Dictionary<int, string>(),
            new System.Collections.Generic.Dictionary<int, string>());

        var restored = snapshot.ToChannels();

        restored.Should().HaveCount(2);
        LiveStreamKind.IsRadio(restored.Single(c => c.StreamId == 1).StreamType).Should().BeTrue();
        LiveStreamKind.IsRadio(restored.Single(c => c.StreamId == 2).StreamType).Should().BeFalse();
    }

    [Fact]
    public void ASnapshotWrittenBeforeThisExistedReadsAsTelevision()
    {
        // An older file simply has no value here, and television is what every earlier version
        // published for everything anyway.
        var entry = new LiveChannelSnapshotEntry { StreamId = 7, Name = "Old" };
        LiveStreamKind.IsRadio(entry.StreamType).Should().BeFalse();
    }
}
