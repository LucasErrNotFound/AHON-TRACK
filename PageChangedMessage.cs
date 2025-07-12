using System;

namespace AHON_TRACK;

public sealed class PageChangedMessage
{
    public required Type PageType { get; init; }
}
