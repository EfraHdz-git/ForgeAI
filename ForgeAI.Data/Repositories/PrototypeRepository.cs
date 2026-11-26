using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class PrototypeRepository : IPrototypeRepository
{
    private readonly DbConnectionFactory _db;

    public PrototypeRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(Prototype prototype)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO prototypes (opportunity_id, evaluation_id, prd_content, local_project_path, deploy_provider, status)
            OUTPUT INSERTED.id
            VALUES (@OpportunityId, @EvaluationId, @PrdContent, @LocalProjectPath, @DeployProvider, @Status)", prototype);
    }

    public async Task<Prototype?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Prototype>(
            "SELECT * FROM prototypes WHERE id = @Id", new { Id = id });
    }

    public async Task<Prototype?> GetByOpportunityIdAsync(int opportunityId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Prototype>(
            "SELECT TOP 1 * FROM prototypes WHERE opportunity_id = @OpportunityId ORDER BY created_at DESC",
            new { OpportunityId = opportunityId });
    }

    public async Task UpdateStatusAsync(int id, string status, string? errorMessage = null)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE prototypes SET status = @Status, error_message = @ErrorMessage,
                build_finished_at = CASE WHEN @Status IN ('deployed','failed') THEN GETUTCDATE() ELSE build_finished_at END
            WHERE id = @Id",
            new { Id = id, Status = status, ErrorMessage = errorMessage });
    }

    public async Task UpdateDeploymentAsync(int id, string deployUrl, string previewPath)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE prototypes SET deploy_url = @DeployUrl, preview_path = @PreviewPath,
                status = 'deployed', deployed_at = GETUTCDATE()
            WHERE id = @Id",
            new { Id = id, DeployUrl = deployUrl, PreviewPath = previewPath });
    }

    public async Task UpdateExecutionMetadataAsync(int id, PrototypeExecutionMetadata metadata)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE prototypes SET
                command_used = @CommandUsed,
                working_directory = @WorkingDirectory,
                exit_code = @ExitCode,
                stdout_log = @StdoutLog,
                stderr_log = @StderrLog,
                generated_files = @GeneratedFiles,
                build_command = @BuildCommand,
                run_command = @RunCommand,
                entry_file = @EntryFile,
                build_ready = @BuildReady,
                build_finished_at = @BuildFinishedAt,
                local_project_path = @WorkingDirectory
            WHERE id = @Id",
            new
            {
                Id = id,
                metadata.CommandUsed,
                metadata.WorkingDirectory,
                metadata.ExitCode,
                metadata.StdoutLog,
                metadata.StderrLog,
                metadata.GeneratedFiles,
                metadata.BuildCommand,
                metadata.RunCommand,
                metadata.EntryFile,
                metadata.BuildReady,
                metadata.BuildFinishedAt
            });
    }
}
