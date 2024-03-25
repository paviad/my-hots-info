using Microsoft.EntityFrameworkCore;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;

namespace MyReplayLibrary;

public class PlayerQuery(ReplayDbContext dc) {
    public async Task<List<ReplayEntry>> QueryByName(string name) {
        var replays = await dc.ReplayCharacters.Where(r => EF.Functions.Like(r.Player.Name, name))
            .Select(r => r.Replay).ToListAsync();

        return replays;
    }
}
