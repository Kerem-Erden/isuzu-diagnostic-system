using System.Text.RegularExpressions;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Data;

public static class DtcKnowledgeMapper
{
    public static DiagnosticTroubleCode ToDiagnosticTroubleCode(
        DtcKnowledgeDetails knowledge,
        string status)
    {
        ArgumentNullException.ThrowIfNull(knowledge);

        List<DtcCause> causes =
            knowledge.PossibleCauses
                .Select(NormalizeWhitespace)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new DtcCause(x))
                .ToList();

        List<DiagnosticStep> diagnosticSteps =
            knowledge.DiagnosticSteps
                .Select(NormalizeWhitespace)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((x, index) =>
                    new DiagnosticStep(index + 1, x))
                .ToList();

        List<DiagnosticStep> confirmationSteps =
            knowledge.ConfirmationSteps
                .Select(NormalizeWhitespace)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((x, index) =>
                    new DiagnosticStep(index + 1, x))
                .ToList();

        List<RelatedLiveDataItem> relatedLiveData =
            knowledge.RelatedLiveData
                .Select(x =>
                    new RelatedLiveDataItem(
                        x.ParameterKey,
                        NormalizeWhitespace(x.DisplayName),
                        x.Unit ?? string.Empty))
                .ToList();

        List<DtcSolution> solutions =
            knowledge.SolutionRecommendations
                .Select(NormalizeWhitespace)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new DtcSolution(x))
                .ToList();

        return new DiagnosticTroubleCode(
            code: knowledge.Code,
            description: BuildDisplayDescription(knowledge),
            status: status,
            possibleCauses: causes,
            diagnosticSteps: diagnosticSteps,
            relatedLiveData: relatedLiveData,
            possibleSolutions: solutions,
            confirmationSteps: confirmationSteps,
            detailedDescription:
                NormalizeWhitespace(knowledge.Description));
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string BuildDisplayDescription(
    DtcKnowledgeDetails knowledge)
    {
        return knowledge.Code.ToUpperInvariant() switch
        {
            "P0087" =>
                "Fuel Rail Pressure Too Low — Absolute rail pressure below threshold",

            "P1093" =>
                "Fuel Rail Pressure Too Low — Actual pressure below desired pressure",

            _ =>
                NormalizeWhitespace(knowledge.Title)
        };
    }
}