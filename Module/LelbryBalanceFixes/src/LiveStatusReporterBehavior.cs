using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace LelbryBalanceFixes
{
    /// <summary>
    /// Two-stage reporter:
    ///   1) Game-thread event handlers (OnSessionLaunched / OnPartySizeChanged / QuarterHourlyTick /
    ///      LiveConfig.Reloaded) update an in-memory <see cref="Snapshot"/> with primitive values.
    ///   2) A background <see cref="Timer"/> flushes the snapshot to live-status.json on disk.
    ///
    /// Why two stages: accessing game state (MainParty.PartySizeLimit etc.) MUST happen on the game
    /// thread, but file I/O is thread-friendly. Doing both inside event handlers — especially during
    /// save-load — was risking blocking the load thread, leading to "infinite loading" after the
    /// "module mismatch" prompt. With this split, even if the game thread hangs we still write
    /// a stale-but-valid status, and the launcher's "Игра не запущена" check works on file mtime.
    /// </summary>
    public sealed class LiveStatusReporterBehavior : CampaignBehaviorBase
    {
        public const string FileName = "live-status.json";
        private const int FlushIntervalMs = 3000;

        private readonly string _moduleDir;
        private Timer _flushTimer;
        private volatile Snapshot _snapshot = new Snapshot();

        public LiveStatusReporterBehavior(string moduleDir)
        {
            _moduleDir = moduleDir;
        }

        public override void RegisterEvents()
        {
            ModLog.Info("LiveStatusReporter.RegisterEvents called.");

            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnPartySizeChangedEvent.AddNonSerializedListener(this, OnPartySizeChanged);
            CampaignEvents.QuarterHourlyTickEvent.AddNonSerializedListener(this, OnTick);

            LiveConfig.Reloaded += OnConfigReloaded;

            // Background flusher — independent of campaign tick. Even if the campaign isn't
            // ticking yet (e.g. still loading), the launcher gets a heartbeat from the
            // last-known snapshot.
            _flushTimer = new Timer(_ => FlushToDisk(), null, FlushIntervalMs, FlushIntervalMs);
        }

        public override void SyncData(IDataStore dataStore) { /* nothing to persist */ }

        // ---- game-thread handlers (just update the cached snapshot) ----

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ModLog.Info("OnSessionLaunched.");
            UpdateSnapshot();
        }

        private void OnPartySizeChanged(PartyBase party) => UpdateSnapshot();
        private void OnTick() => UpdateSnapshot();
        private void OnConfigReloaded() => UpdateSnapshot();

        private void UpdateSnapshot()
        {
            try
            {
                var main = Campaign.Current?.MainParty;
                if (main?.Party == null)
                {
                    _snapshot = new Snapshot { HasParty = false, AppliedBonus = LiveConfig.PartySizeBonus };
                    return;
                }

                int memberCount = 0;
                int limit = 0;
                try { memberCount = main.MemberRoster?.TotalManCount ?? 0; } catch { }
                try { limit = main.Party.PartySizeLimit; } catch { }

                _snapshot = new Snapshot
                {
                    HasParty = true,
                    MemberCount = memberCount,
                    Limit = limit,
                    AppliedBonus = LiveConfig.PartySizeBonus
                };
            }
            catch (Exception ex)
            {
                ModLog.Error("UpdateSnapshot: " + ex.Message);
            }
        }

        // ---- background-thread writer (NO game-state access) ----

        private void FlushToDisk()
        {
            try
            {
                if (string.IsNullOrEmpty(_moduleDir)) return;
                var snap = _snapshot;

                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"ts\":\"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
                if (snap != null && snap.HasParty)
                {
                    int vanilla = snap.Limit - snap.AppliedBonus;
                    sb.Append("\"mainParty\":{");
                    sb.Append("\"memberCount\":").Append(snap.MemberCount).Append(",");
                    sb.Append("\"limit\":").Append(snap.Limit).Append(",");
                    sb.Append("\"vanillaLimit\":").Append(vanilla).Append(",");
                    sb.Append("\"appliedBonus\":").Append(snap.AppliedBonus);
                    sb.Append("}");
                }
                else
                {
                    sb.Append("\"mainParty\":null");
                }
                sb.Append("}");

                var path = Path.Combine(_moduleDir, FileName);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                ModLog.Error("FlushToDisk: " + ex.Message);
            }
        }

        private sealed class Snapshot
        {
            public bool HasParty;
            public int MemberCount;
            public int Limit;
            public int AppliedBonus;
        }
    }
}
