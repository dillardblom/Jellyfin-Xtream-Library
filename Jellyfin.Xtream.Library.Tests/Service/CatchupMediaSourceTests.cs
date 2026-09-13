// Copyright (C) 2024  Roland Breitschaft

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #108. A finished programme is finite, which is exactly where it differs from the live
// tuner's media source. Copying the live shape would give a stream with no end and no runtime,
// and a client with no seek bar.

using System;
using FluentAssertions;
using Jellyfin.Xtream.Library.Service;
using MediaBrowser.Model.MediaInfo;
using Xunit;

namespace Jellyfin.Xtream.Library.Tests.Service;

public class CatchupMediaSourceTests
{
    private static readonly CatchupItemRef OneHour =
        new(CatchupItemKind.Programme, 0, 4711, 0, 1_700_000_000, 1_700_003_600);

    [Fact]
    public void ItIsNotAnInfiniteStream()
    {
        // The live tuner sets this true. A recording ends.
        XtreamCatchupChannel.BuildMediaSource("id", "http://host/x.ts", OneHour)
            .IsInfiniteStream.Should().BeFalse();
    }

    [Fact]
    public void TheRuntimeComesFromTheProgrammesOwnWindow()
    {
        // Without a runtime a client cannot draw a seek bar, which is most of the point of being
        // able to watch something that already aired.
        XtreamCatchupChannel.BuildMediaSource("id", "http://host/x.ts", OneHour)
            .RunTimeTicks.Should().Be(TimeSpan.FromHours(1).Ticks);
    }

    [Fact]
    public void ItCarriesNoPlaceholderStreams()
    {
        // The live path invents a video and an audio entry to skip probing. Doing that here makes
        // Jellyfin skip the probe as well, and a finite file is cheap to probe properly.
        var source = XtreamCatchupChannel.BuildMediaSource("id", "http://host/x.ts", OneHour);
        source.MediaStreams.Should().BeNullOrEmpty();
        source.SupportsProbing.Should().BeTrue();
    }

    [Fact]
    public void ItIsServedOverHttpAsTransportStream()
    {
        var source = XtreamCatchupChannel.BuildMediaSource("id", "http://host/x.ts", OneHour);
        source.Protocol.Should().Be(MediaProtocol.Http);
        source.Container.Should().Be("mpegts");
        source.IsRemote.Should().BeTrue();
    }

    [Fact]
    public void TheSourceIsIdentifiedByTheItemItBelongsTo()
    {
        XtreamCatchupChannel.BuildMediaSource("xcu|p|0|4711|1|2", "http://host/x.ts", OneHour)
            .Id.Should().Be("xcu|p|0|4711|1|2");
    }
}
