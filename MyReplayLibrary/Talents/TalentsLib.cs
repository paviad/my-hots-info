using System.Xml.Serialization;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;
using MyReplayLibrary.Talents.Options;

namespace MyReplayLibrary.Talents;

public static class TalentsLib {
    public static bool DeleteInternal(DeleteOptions opts, ReplayDbContext dc) {
        var q = dc.HeroTalentInformations
            .Where(x => x.ReplayBuildFirst == opts.Build)
            .Where(x => x.Character == opts.Hero)
            .AsQueryable();

        var talents = q.ToList();

        if (talents.Any(x => x.ReplayBuildFirst == opts.Build)) {
            return true;
        }

        var range = dc.HeroTalentInformations
            .Where(x => x.Character == opts.Hero)
            .FirstOrDefault(x => x.ReplayBuildFirst <= opts.Build && x.ReplayBuildLast >= opts.Build);
        Console.Error.Write($"Build {opts.Build} doesn't match the start of a build range, ");
        Console.Error.Write(
            range != null
                ? $"it falls in range {range.ReplayBuildFirst}-{range.ReplayBuildLast}."
                : "and no range contains it.");
        return false;
    }

    public static Dictionary<int, List<DiffEntry>> DiffInternal(DiffOptions opts, ReplayDbContext dc) {
        var q = dc.HeroTalentInformations.AsQueryable();
        if (!string.IsNullOrEmpty(opts.Hero)) {
            q = q.Where(x => x.Character == opts.Hero);
        }

        if (opts.MinBuild.HasValue) {
            var minBuild = opts.MinBuild.Value;
            q = q.Where(x => x.ReplayBuildLast >= minBuild);
        }

        if (opts.MaxBuild.HasValue) {
            var maxBuild = opts.MaxBuild.Value;
            q = q.Where(x => x.ReplayBuildFirst <= maxBuild);
        }

        if (opts.TalentId is not null) {
            var talentId = opts.TalentId;
            q = q.Where(x => x.TalentId == talentId);
        }

        var talents = q.ToList();
        var byHeroByRange = talents
            .ToLookup(x => x.Character)
            .ToDictionary(x => x.Key, x => x.GroupBy(y => y.ReplayBuildFirst));

        var diffList = new Dictionary<int, List<DiffEntry>>();

        foreach (var kvp in byHeroByRange) {
            foreach (var range in kvp.Value.Triplewise((a, b, c) => (a, b, c))) {
                foreach (var t in range.b!) {
                    var matchPrev = range.a?.SingleOrDefault(x => x.TalentName == t.TalentName);
                    var matchNext1 = range.c?.Where(x => x.TalentName == t.TalentName).ToList();
                    if (matchNext1?.Count > 1) {
                        matchNext1 = matchNext1.Where(x => x.TalentTier == t.TalentTier).ToList();
                    }

                    var matchNext = matchNext1?.SingleOrDefault();

                    if (matchNext == null) {
                        var extendedToNext = range.c?.Any(x => x.ReplayBuildLast == t.ReplayBuildLast) ?? false;
                        if (t.ReplayBuildLast == 1000000 || extendedToNext) {
                            matchNext = t;
                        }
                    }

                    DiffEntry? diffEntry = null;

                    void SetDiffExists(Action<DiffEntry> act) {
                        if (diffEntry == null) {
                            var nextRange = matchNext?.ReplayBuildFirst ?? range.c?.FirstOrDefault()?.ReplayBuildFirst;
                            diffEntry = new DiffEntry {
                                Talent = t,
                                NextTalent = matchNext,
                                NextRange = nextRange,
                            };
                        }

                        act(diffEntry);
                    }

                    if (range.a != null && matchPrev == null) {
                        SetDiffExists(x => x.IsNew = true);
                    }

                    if (matchNext != null) {
                        if (matchNext.TalentTier != t.TalentTier) {
                            SetDiffExists(x => x.IsTierChanged = true);
                        }

                        if (matchNext.TalentDescription != t.TalentDescription &&
                            opts.OutputType == OutputType.Extended) {
                            SetDiffExists(x => x.IsDescriptionChanged = true);
                        }
                    }
                    else {
                        SetDiffExists(x => x.IsRemovedInNext = true);
                    }

                    if (diffEntry != null) {
                        if (!diffList.TryGetValue(t.ReplayBuildFirst, out var value)) {
                            value = [];
                            diffList[t.ReplayBuildFirst] = value;
                        }

                        value.Add(diffEntry);
                    }
                }
            }
        }

        return diffList;
    }

    public static void ExtendInternal(ExtendOptions opts, ReplayDbContext dc) {
        var builds = dc.BuildNumbers.Select(x => x.Buildnumber1).OrderBy(x => x).ToList();
        var q = dc.HeroTalentInformations
            .Where(
                x => (x.ReplayBuildFirst <= opts.MinBuild && x.ReplayBuildLast >= opts.MinBuild) ||
                     (x.ReplayBuildFirst <= opts.MaxBuild && x.ReplayBuildFirst > opts.MinBuild))
            .AsQueryable();

        if (!string.IsNullOrEmpty(opts.Hero)) {
            q = q.Where(x => x.Character == opts.Hero);
        }

        if (opts.TalentId is not null) {
            var talentId = opts.TalentId;
            q = q.Where(x => x.TalentId == talentId);
        }

        var talents = q.ToList();

        var startTalents = talents
            .Where(x => x.ReplayBuildFirst <= opts.MinBuild && x.ReplayBuildLast >= opts.MinBuild).ToList();

        var toRemove = talents
            .Except(startTalents)
            .Where(x => x.ReplayBuildFirst <= opts.MaxBuild && x.ReplayBuildLast <= opts.MaxBuild).ToList();

        var toSplit = talents
            .Except(startTalents)
            .Where(x => x.ReplayBuildFirst <= opts.MaxBuild && x.ReplayBuildLast > opts.MaxBuild).ToList();

        var trueLast = builds.Last(x => x <= opts.MaxBuild);

        startTalents.ForEach(x => {
            x.ReplayBuildLast = trueLast;
        });

        dc.HeroTalentInformations.RemoveRange(toRemove);

        var nextBuild = builds.First(x => x > opts.MaxBuild);

        foreach (var t in toSplit) {
            var newTalent = new HeroTalentInformation {
                ReplayBuildFirst = nextBuild,
                ReplayBuildLast = t.ReplayBuildLast,
                Character = t.Character,
                TalentId = t.TalentId,
                TalentDescription = t.TalentDescription,
                TalentTier = t.TalentTier,
                TalentName = t.TalentName,
            };
            dc.HeroTalentInformations.Add(newTalent);
        }

        dc.HeroTalentInformations.RemoveRange(toSplit);
    }

    public static List<HeroTalentInformation> GetInternal(GetOptions opts, ReplayDbContext dc) {
        var latest = false;
        var q = dc.HeroTalentInformations.AsQueryable();
        if (!string.IsNullOrEmpty(opts.Hero)) {
            q = q.Where(x => x.Character == opts.Hero);
        }

        if (!string.IsNullOrEmpty(opts.Build)) {
            if (opts.Build == "latest") {
                latest = true;
            }
            else {
                var build = opts.Build == "current" ? 1000000 : int.Parse(opts.Build);
                q = q.Where(x => x.ReplayBuildFirst <= build && x.ReplayBuildLast >= build);
            }
        }

        if (opts.MinBuild.HasValue) {
            var minBuild = opts.MinBuild.Value;
            q = q.Where(x => x.ReplayBuildLast >= minBuild);
        }

        if (opts.MaxBuild.HasValue) {
            var maxBuild = opts.MaxBuild.Value;
            q = q.Where(x => x.ReplayBuildFirst <= maxBuild);
        }

        if (opts.TalentId is not null) {
            var talentId = opts.TalentId;
            q = q.Where(x => x.TalentId == talentId);
        }

        var talents = q.ToList();

        if (latest) {
            var g = talents.GroupBy(
                    x => new {
                        x.Character,
                        x.TalentId,
                    },
                    x => x.ReplayBuildLast)
                .ToDictionary(x => (x.Key.Character, x.Key.TalentId), x => x.Max());

            talents = talents.Where(x => x.ReplayBuildLast == g[(x.Character, x.TalentId)]).ToList();
        }

        return talents;
    }

    public static bool InsertInternal(InsertOptions opts, ReplayDbContext dc) {
        var q = dc.HeroTalentInformations
            .Where(
                x => x.ReplayBuildFirst == opts.Build ||
                     ((opts.IncludeLater ?? false) && x.ReplayBuildFirst > opts.Build))
            .Where(x => x.Character == opts.Hero)
            .AsQueryable();

        var talents = q.ToList();

        if (talents.All(x => x.ReplayBuildFirst != opts.Build)) {
            var range = dc.HeroTalentInformations
                .Where(x => x.Character == opts.Hero)
                .FirstOrDefault(x => x.ReplayBuildFirst <= opts.Build && x.ReplayBuildLast >= opts.Build);
            Console.Error.Write($"Build {opts.Build} doesn't match the start of a build range, ");
            Console.Error.Write(
                range != null
                    ? $"it falls in range {range.ReplayBuildFirst}-{range.ReplayBuildLast}."
                    : "and no range contains it.");
            {
                return false;
            }
        }

        var ranges = talents
            .Select(x => (x.ReplayBuildFirst, x.ReplayBuildLast))
            .Distinct()
            .GroupBy(x => x.ReplayBuildFirst)
            .ToDictionary(x => x.Key, x => x.Max(y => y.ReplayBuildLast))
            .Select(kvp => (ReplayBuildFirst: kvp.Key, ReplayBuildLast: kvp.Value));
        foreach (var (replayBuildFirst, replayBuildLast) in ranges) {
            var newTalent = new HeroTalentInformation {
                ReplayBuildFirst = replayBuildFirst,
                ReplayBuildLast = replayBuildLast,
                Character = opts.Hero,
                TalentId = opts.TalentId,
                TalentDescription = opts.TalentDescription,
                TalentTier = opts.TalentTier,
                TalentName = opts.TalentName,
            };
            dc.HeroTalentInformations.Add(newTalent);
        }

        return true;
    }

    public static void NewBuildInternal(NewBuildOptions opts, ReplayDbContext dc) {
        int[] tierLevels = [1, 4, 7, 10, 13, 16, 20];
        var ser = new XmlSerializer(typeof(TalentUpdatePackage));
        using var f = File.OpenText(opts.Path);
        var tup = (TalentUpdatePackage?)ser.Deserialize(f) ??
                  throw new InvalidOperationException("Couldn't deserialize talent info xml");
        var talentInfoList = tup.TalentInfoList;

        var alarakCounterStrike20 =
            talentInfoList.Single(x => x.HeroName == "Alarak" && x is { Tier: 7, TalentName: "Counter-Strike" });
        alarakCounterStrike20.TalentName += " (Lvl 20)";
        var alarakDeadlyCharge20 =
            talentInfoList.Single(x => x.HeroName == "Alarak" && x is { Tier: 7, TalentName: "Deadly Charge" });
        alarakDeadlyCharge20.TalentName += " (Lvl 20)";

        var currentTalents = GetInternal(
            new GetOptions {
                Build = "current",
            },
            dc);
        var dicCurrentRef = currentTalents.ToDictionary(x => (x.Character, x.TalentId));
        var dicCurrent = currentTalents.ToDictionary(
            x => (x.Character, x.TalentId),
            x => (x.TalentName, x.TalentTier, x.TalentDescription));
        var dicNewRef = talentInfoList.ToDictionary(x => (x.HeroName, x.TalentId));
        var dicNew = talentInfoList.ToDictionary(
            x => (x.HeroName, x.TalentId),
            x => (x.TalentName, TalentTier: tierLevels[x.Tier - 1], x.TalentDescription));
        var joinKeys = dicCurrent.Keys.Intersect(dicNew.Keys).ToList();
        var changed = joinKeys.Where(x => dicNew[x] != dicCurrent[x]).ToList();
        var newTalents = dicNew.Keys.Except(dicCurrent.Keys);
        var removedTalents = dicCurrent.Keys.Except(dicNew.Keys);

        var builds = dc.BuildNumbers.OrderBy(x => x).ToList();

        if (builds.All(x => x.Buildnumber1 != tup.Build)) {
            var newBuildNumber = new BuildNumber {
                Builddate = tup.Date,
                Buildnumber1 = tup.Build,
                Version = tup.Version,
            };
            dc.BuildNumbers.Add(newBuildNumber);
        }

        var lastBuild = builds.LastOrDefault(x => x.Buildnumber1 < tup.Build)?.Buildnumber1 ?? 0;

        foreach (var v in changed) {
            var dbTalent = dicCurrentRef[v];
            var newRef = dicNewRef[v];
            dbTalent.ReplayBuildLast = lastBuild;
            var dbUpdateTalent = new HeroTalentInformation {
                TalentDescription = newRef.TalentDescription,
                TalentTier = tierLevels[newRef.Tier - 1],
                TalentName = newRef.TalentName,
                TalentId = newRef.TalentId,
                Character = newRef.HeroName,
                ReplayBuildFirst = tup.Build,
                ReplayBuildLast = 1000000,
            };

            dc.HeroTalentInformations.Add(dbUpdateTalent);
        }

        foreach (var v in newTalents) {
            var newRef = dicNewRef[v];
            var dbTalent = new HeroTalentInformation {
                TalentDescription = newRef.TalentDescription,
                TalentTier = tierLevels[newRef.Tier - 1],
                TalentName = newRef.TalentName,
                TalentId = newRef.TalentId,
                Character = newRef.HeroName,
                ReplayBuildFirst = tup.Build,
                ReplayBuildLast = 1000000,
            };

            dc.HeroTalentInformations.Add(dbTalent);
        }

        foreach (var v in removedTalents) {
            var dbTalent = dicCurrentRef[v];
            dbTalent.ReplayBuildLast = lastBuild;
        }
    }

    public static void SplitInternal(SplitOptions opts, ReplayDbContext dc) {
        // say we got a range of 0-2000 and we want to split it to 0-400 and 500-2000
        // we want all ranges that include 400 and 500, so all ranges where first<400
        // or last>500. So if we got a range of 400-450 we dont touch it but if we
        // got a range of 400-600 we want 400-400 and 500-600

        var q = dc.HeroTalentInformations
            .Where(
                x =>
                    (x.ReplayBuildFirst < opts.MinBuild && x.ReplayBuildLast > opts.MinBuild) ||
                    (x.ReplayBuildFirst < opts.MaxBuild && x.ReplayBuildLast > opts.MaxBuild))
            .AsQueryable();

        if (!string.IsNullOrEmpty(opts.Hero)) {
            q = q.Where(x => x.Character == opts.Hero);
        }

        if (opts.TalentId is not null) {
            var talentId = opts.TalentId;
            q = q.Where(x => x.TalentId == talentId);
        }

        var talents = q.ToList();

        var newTalents = talents.Select(
            x => new HeroTalentInformation {
                ReplayBuildFirst = opts.MaxBuild,
                ReplayBuildLast = x.ReplayBuildLast,
                Character = x.Character,
                TalentId = x.TalentId,
                TalentDescription = x.TalentDescription,
                TalentTier = x.TalentTier,
                TalentName = x.TalentName,
            });

        dc.HeroTalentInformations.AddRange(newTalents);

        talents.ForEach(x => {
            x.ReplayBuildLast = opts.MinBuild;
        });
    }
}
