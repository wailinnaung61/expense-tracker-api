using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

/// <summary>
/// In-process export (same logic as export-lambda). Used when Export:Mode = Local.
/// </summary>
public interface ILocalExportProcessor
{
    /// <summary>Queue background work; returns immediately so API can respond PENDING.</summary>
    void Enqueue(ExportEventDetail detail);
}
