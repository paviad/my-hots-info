using System.Xml.Serialization;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;
using MyReplayLibrary.Talents.Options;

namespace MyReplayLibrary.Talents;

public static class TalentsLib {
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

    public static void NewBuildInternal(NewBuildOptions opts, ReplayDbContext dc) {
        int[] tierLevels = [1, 4, 7, 10, 13, 16, 20];
        var ser = new XmlSerializer(typeof(TalentUpdatePackage));
        using var f = File.OpenText(opts.Path!);
        var tup = (TalentUpdatePackage?)ser.Deserialize(f) ??
                  throw new InvalidOperationException("Couldn't deserialize talent info xml");
        var talentInfoList = tup.TalentInfoList;

        var alarakCounterStrike20 =
            talentInfoList.SingleOrDefault(x => x.HeroName == "Alarak" && x is { Tier: 7, TalentName: "Counter-Strike" });
        
        if(alarakCounterStrike20 is not null) {
            alarakCounterStrike20.TalentName += " (Lvl 20)";
        }
        
        var alarakDeadlyCharge20 =
            talentInfoList.SingleOrDefault(x => x.HeroName == "Alarak" && x is { Tier: 7, TalentName: "Deadly Charge" });

        if (alarakDeadlyCharge20 is not null) {
            alarakDeadlyCharge20.TalentName += " (Lvl 20)";
        }

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
}
