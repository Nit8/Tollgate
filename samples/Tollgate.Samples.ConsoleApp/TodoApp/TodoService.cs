using Tollgate.Abstractions;
using Tollgate.Abstractions.Enums;

// ─────────────────────────────────────────────────────────────
//  Sample TODO service — demonstrates [RequireFeature] / [RequireTier].
//
//  This is the kind of code your customer (the developer who installs
//  your NuGet package) writes in their own app. Tollgate enforces
//  the annotations via LicenseGate.EnsureAccessFor(MethodBase).
// ─────────────────────────────────────────────────────────────

namespace Tollgate.Samples.ConsoleApp.TodoApp
{
    public class TodoService
    {
        private readonly List<string> _todos = new();

        // ── FREE: anyone can list & read ────────────────────────────
        public IReadOnlyList<string> List() => _todos;

        public string? Get(int i) =>
            i >= 0 && i < _todos.Count ? _todos[i] : null;

        // ── BASIC: requires Basic tier ──────────────────────────────
        [RequireTier(LicenseTier.Basic,
            DeniedMessage = "Adding todos requires the Basic tier.")]
        public void Add(string todo) =>
            _todos.Add(todo);

        [RequireTier(LicenseTier.Basic,
            DeniedMessage = "Deleting todos requires the Basic tier.")]
        public void Delete(int i)
        {
            if (i >= 0 && i < _todos.Count) _todos.RemoveAt(i);
        }

        // ── PRO: requires Pro tier ───────────────────────────────────
        [RequireTier(LicenseTier.Pro,
            DeniedMessage = "Bulk import requires the Pro tier.")]
        public void BulkImport(IEnumerable<string> todos) =>
            _todos.AddRange(todos);

        // ── FEATURE FLAG: requires 'export-pdf' feature (any tier) ───
        [RequireFeature("export-pdf",
            DeniedMessage = "Exporting to PDF requires the 'export-pdf' feature.")]
        public byte[] ExportToPdf() =>
            System.Text.Encoding.UTF8.GetBytes("PDF content placeholder: " + string.Join("\n", _todos));

        // ── FEATURE FLAG: requires 'ai-assist' feature ───────────────
        [RequireFeature("ai-assist",
            DeniedMessage = "AI assist requires the 'ai-assist' feature.")]
        public string SuggestNextTask() =>
            "Buy groceries (suggested by Tollgate-AI)";
    }
}
