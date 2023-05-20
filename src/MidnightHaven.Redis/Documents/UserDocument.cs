﻿using NodaTime;
using Redis.OM.Modeling;

namespace MidnightHaven.Redis.Documents;

[Document(StorageType = StorageType.Json, Prefixes = new []{"user"})]
public class UserDocument : Document
{
    private const string VrcUsrUrl = "https://vrchat.com/home/user/";

    [Indexed]
    [RedisIdField]
    public override ulong Id { get; set; }

    [Indexed]
    public string? VrcUserId { get; set; }

    [Indexed]
    public DateTimeZone? Timezone { get; set; }

    [Indexed]
    public bool EnableBirthday { get; set; } = false;

    [Indexed]
    public AnnualDate? AnnualBirthdate { get; set; }

    [Indexed]
    public LocalDate? LastBirthdateAnnouncement { get; set; }

    public string? GetVrcUsrUrl() => VrcUserId is not null ? VrcUsrUrl + VrcUserId : null;
    public string? GetHyperLinkedVrcUsrUrl(string? hyperLinkText = null)
    {
        var usrUrl = GetVrcUsrUrl();
        return usrUrl is not null ?  $"[{hyperLinkText ?? usrUrl}]({GetVrcUsrUrl()})" : null;
    }
}
