using PeFix.Plan;

namespace PeFix.Patch;

internal static class VerifiedWrite
{
    internal static VerifiedWriteResult Apply(Request request)
    {
        return ApplyBatch([request])[0];
    }

    internal static string? ApplyNoPlan(NoPlan request)
    {
        string? backupPath = request.Backup ? PeUtils.Backup(request.Path) : null;
        Write(request.Path, request.Patched, request.Verify);
        return backupPath;
    }

    internal static void Preflight(string path, bool backup)
    {
        string sidecarPath = PlanEmit.SidecarPath(path);
        if (Directory.Exists(sidecarPath))
            throw new IOException($"Plan path {sidecarPath} is a directory.");

        string sidecarTemporaryPath = $"{sidecarPath}.tmp.{Environment.ProcessId}";
        if (Directory.Exists(sidecarTemporaryPath))
            throw new IOException($"Plan temp path {sidecarTemporaryPath} is a directory.");

        string targetTemporaryPath = $"{path}.tmp.{Environment.ProcessId}";
        if (Directory.Exists(targetTemporaryPath))
            throw new IOException($"Target temp path {targetTemporaryPath} is a directory.");

        string backupPath = path + ".bak";
        if (backup && File.Exists(backupPath))
            throw new IOException($"Backup file {backupPath} already exists. Remove it or run with --no-backup.");
    }

    internal static VerifiedWriteResult[] ApplyBatch(IReadOnlyList<Request> requests)
    {
        if (requests.Count == 0)
            return [];

        BatchState[] batch = BuildBatch(requests);
        try
        {
            StageBatch(batch);
            BackupBatch(batch);
            CommitBatch(batch);
            return [.. batch.Select(item => new VerifiedWriteResult(item.BackupPath, item.SidecarPath))];
        }
        catch (Exception applyError)
        {
            Exception[] errors = RollbackBatch(batch);
            if (errors.Length > 0)
                throw new AggregateException(
                    "Batch write failed; rollback also failed.",
                    new[] { applyError }.Concat(errors));
            throw;
        }
        finally
        {
            DeleteTemps(batch);
        }
    }

    private static BatchState[] BuildBatch(IReadOnlyList<Request> requests)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        List<BatchState> batch = [];
        foreach (Request request in requests)
        {
            if (!paths.Add(request.Path))
                throw new InvalidOperationException($"Duplicate write target in batch: {request.Path}");

            Preflight(request.Path, request.Backup);
            string? backupPath = request.Backup ? request.Path + ".bak" : null;
            string sidecarPath = PlanEmit.SidecarPath(request.Path);
            batch.Add(new BatchState(request, backupPath, sidecarPath, CaptureSidecar(sidecarPath)));
        }
        return [.. batch];
    }

    private static void StageBatch(BatchState[] batch)
    {
        foreach (BatchState item in batch)
        {
            item.TargetTmp = StageTarget(item.Request.Path, item.Request.Patched, item.Request.Verify);
            item.PlanTmp = StagePlan(item.Request, item.BackupPath);
        }
    }

    private static string StagePlan(Request request, string? backupPath)
    {
        PefixPlan plan = PlanEmit.Create(new PlanEmit.Request
        {
            Input = PlanFileInfo.Describe(request.Path, request.Original, PeUtils.ReadMvid(request.Original)),
            Output = PlanFileInfo.Describe(request.Path, request.Patched, PeUtils.ReadMvid(request.Patched)),
            Ops = request.Ops,
            BackupPath = backupPath
        });
        return PlanEmit.Stage(request.Path, plan);
    }

    private static void BackupBatch(BatchState[] batch)
    {
        foreach (BatchState item in batch)
        {
            if (item.BackupPath is null) continue;
            PeUtils.Backup(item.Request.Path);
            item.BackupDone = true;
        }
    }

    private static void CommitBatch(BatchState[] batch)
    {
        foreach (BatchState item in batch)
            CommitItem(item);
    }

    private static void CommitItem(BatchState item)
    {
        PlanEmit.Commit(item.Request.Path, item.PlanTmp!);
        item.PlanTmp = null;
        item.PlanDone = true;
        PeUtils.Commit(item.TargetTmp!, item.Request.Path);
        item.TargetTmp = null;
        item.TargetDone = true;
    }

    private static void Write(string path, byte[] patched, Action<string> verify)
    {
        PeUtils.WriteVerifiedAtomic(path, patched, tmpPath =>
        {
            verify(tmpPath);
            Validator.Validate(tmpPath);
        });
    }

    private static string StageTarget(string path, byte[] patched, Action<string> verify)
    {
        return PeUtils.StageVerified(path, patched, tmpPath =>
        {
            verify(tmpPath);
            Validator.Validate(tmpPath);
        });
    }

    private static SidecarState CaptureSidecar(string path)
    {
        return File.Exists(path)
            ? new SidecarState(true, File.ReadAllBytes(path))
            : new SidecarState(false, []);
    }

    private static Exception[] RollbackBatch(BatchState[] batch)
    {
        List<Exception> errors = [];
        foreach (BatchState item in batch.Reverse())
            RollbackItem(item, errors);
        return [.. errors];
    }

    private static void RollbackItem(BatchState item, List<Exception> errors)
    {
        if (item.TargetDone)
            TryRollback(new RollbackStep(
                () => File.WriteAllBytes(item.Request.Path, item.Request.Original),
                item.Request.Path,
                "target"), errors);

        if (item.PlanDone)
            TryRollback(new RollbackStep(
                () => RestoreSidecar(item.SidecarPath, item.OldSidecar),
                item.SidecarPath,
                "plan"), errors);

        if (item.BackupDone && item.BackupPath is not null && File.Exists(item.BackupPath))
            TryRollback(new RollbackStep(
                () => File.Delete(item.BackupPath),
                item.BackupPath,
                "backup"), errors);
    }

    private static void TryRollback(RollbackStep step, List<Exception> errors)
    {
        try
        {
            step.Action();
        }
        catch (IOException ex) { AddRollbackError(step, ex, errors); }
        catch (UnauthorizedAccessException ex) { AddRollbackError(step, ex, errors); }
        catch (NotSupportedException ex) { AddRollbackError(step, ex, errors); }
    }

    private static void AddRollbackError(RollbackStep step, Exception ex, List<Exception> errors)
    {
        errors.Add(new InvalidOperationException($"Rollback failed for {step.Part} '{step.Path}'.", ex));
    }

    private static void RestoreSidecar(string path, SidecarState oldSidecar)
    {
        if (oldSidecar.Exists)
        {
            File.WriteAllBytes(path, oldSidecar.Bytes);
            return;
        }

        if (File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteTemps(BatchState[] batch)
    {
        foreach (BatchState item in batch)
        {
            PeUtils.DeleteTemporaryFile(item.TargetTmp);
            PlanEmit.DeleteTemporaryFile(item.PlanTmp);
        }
    }

    internal sealed class Request
    {
        public required string Path { get; init; }
        public required byte[] Original { get; init; }
        public required byte[] Patched { get; init; }
        public required IReadOnlyList<MutationOp> Ops { get; init; }
        public required bool Backup { get; init; }
        public required Action<string> Verify { get; init; }
    }

    internal sealed class NoPlan
    {
        public required string Path { get; init; }
        public required byte[] Patched { get; init; }
        public required bool Backup { get; init; }
        public required Action<string> Verify { get; init; }
    }

    private sealed class BatchState(
        Request request,
        string? backupPath,
        string sidecarPath,
        SidecarState oldSidecar)
    {
        public Request Request { get; } = request;
        public string? BackupPath { get; } = backupPath;
        public string SidecarPath { get; } = sidecarPath;
        public SidecarState OldSidecar { get; } = oldSidecar;
        public string? TargetTmp { get; set; }
        public string? PlanTmp { get; set; }
        public bool BackupDone { get; set; }
        public bool PlanDone { get; set; }
        public bool TargetDone { get; set; }
    }

    private readonly record struct SidecarState(bool Exists, byte[] Bytes);

    private readonly record struct RollbackStep(Action Action, string Path, string Part);
}
