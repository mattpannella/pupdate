using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.AiCheck;

namespace Pannella.Services;

public partial class CoresService
{
    private const string AI_REPORT_END_POINT = "https://openfpga-library.github.io/openfpga-ai-check/ai_report.json";

    private Dictionary<string, AiCheckEntry> aiReport;

    public Dictionary<string, AiCheckEntry> AiReport
    {
        get
        {
            if (aiReport == null)
            {
                try
                {
                    string json = HttpHelper.Instance.GetHTML(AI_REPORT_END_POINT);

                    aiReport = string.IsNullOrWhiteSpace(json)
                        ? new Dictionary<string, AiCheckEntry>()
                        : JsonConvert.DeserializeObject<Dictionary<string, AiCheckEntry>>(json)
                          ?? new Dictionary<string, AiCheckEntry>();
                }
                catch (Exception ex)
                {
                    WriteMessage("Could not load the AI core report; AI core filtering will not be applied this run.");
                    WriteMessage(this.settingsService.Debug.show_stack_traces
                        ? ex.ToString()
                        : Util.GetExceptionMessage(ex));

                    aiReport = new Dictionary<string, AiCheckEntry>();
                }
            }

            return aiReport;
        }
    }

    public static bool ExceedsAiThreshold(double overallScore, int thresholdPercent)
    {
        return overallScore > thresholdPercent / 100.0;
    }

    public bool IsAiFiltered(string identifier)
    {
        if (!this.settingsService.Config.filter_ai_cores)
            return false;

        if (!this.AiReport.TryGetValue(identifier, out var entry) || entry == null)
            return false;

        return ExceedsAiThreshold(entry.overall_score, this.settingsService.Config.ai_core_threshold);
    }
}
