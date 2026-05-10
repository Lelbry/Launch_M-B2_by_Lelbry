using System;
using System.Globalization;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace LelbryBalanceFixes
{
    /// <summary>
    /// Writes live-status.json next to the mod so the launcher can show
    /// the player what the current effective party size limit is in-game.
    /// </summary>
    public sealed class LiveStatusReporterBehavior : CampaignBehaviorBase
    {
        public const string FileName = "live-status.json";

        private string _moduleDir;

        public LiveStatusReporterBehavior(string moduleDir)
        {
            _moduleDir = moduleDir;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnPartySizeChangedEvent.AddNonSerializedListener(this, OnPartySizeChanged);
            CampaignEvents.QuarterHourlyTickEvent.AddNonSerializedListener(this, WriteOnce);

            // wire up live-config reload → also push a fresh status snapshot
            LiveConfig.Reloaded += WriteOnce;
        }

        public override void SyncData(IDataStore dataStore) { /* nothing to persist */ }

        private void OnSessionLaunched(CampaignGameStarter starter) => WriteOnce();
        private void OnPartySizeChanged(PartyBase party) => WriteOnce();

        public void WriteOnce()
        {
            try
            {
                if (string.IsNullOrEmpty(_moduleDir)) return;

                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"ts\":\"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",");

                var main = Campaign.Current?.MainParty;
                if (main != null && main.Party != null)
                {
                    int memberCount = main.MemberRoster?.TotalManCount ?? 0;
                    int limit = main.Party.PartySizeLimit;
                    int appliedBonus = LiveConfig.PartySizeBonus;
                    int vanillaLimit = limit - appliedBonus;
                    sb.Append("\"mainParty\":{");
                    sb.Append("\"memberCount\":").Append(memberCount).Append(",");
                    sb.Append("\"limit\":").Append(limit).Append(",");
                    sb.Append("\"vanillaLimit\":").Append(vanillaLimit).Append(",");
                    sb.Append("\"appliedBonus\":").Append(appliedBonus);
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
                ModLog.Error("LiveStatusReporter.WriteOnce: " + ex.Message);
            }
        }
    }
}
