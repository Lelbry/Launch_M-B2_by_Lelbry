using System;
using System.Text.Json.Serialization;

namespace Launcher.Models;

public sealed class LiveStatus
{
    [JsonPropertyName("ts")]
    public DateTime Ts { get; set; }

    [JsonPropertyName("mainParty")]
    public MainPartyStatus? MainParty { get; set; }
}

public sealed class MainPartyStatus
{
    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("vanillaLimit")]
    public int VanillaLimit { get; set; }

    [JsonPropertyName("appliedBonus")]
    public int AppliedBonus { get; set; }
}
