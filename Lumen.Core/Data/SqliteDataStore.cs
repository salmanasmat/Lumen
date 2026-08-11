using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Lumen.Core.Models;

namespace Lumen.Core.Data;

public class SqliteDataStore
{
    private readonly string _dbPath;

    public SqliteDataStore(string? customDbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            _dbPath = customDbPath;
        }
        else
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");
            Directory.CreateDirectory(folder);
            _dbPath = Path.Combine(folder, "lumen.db");
        }
    }

    private string GetConnectionString() => $"Data Source={_dbPath}";

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS ScanResults (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                SnapshotJson TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Sessions (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                Description TEXT NOT NULL,
                SystemRestorePointCreated INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Actions (
                Id TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                Module TEXT NOT NULL,
                ActionType TEXT NOT NULL,
                TargetName TEXT NOT NULL,
                Details TEXT,
                BeforeStateJson TEXT,
                IsReversible INTEGER NOT NULL,
                IsUndone INTEGER NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
            );
        ";
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveScanResultAsync(DiagnosticsSnapshot snapshot)
    {
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ScanResults (Id, Timestamp, SnapshotJson)
            VALUES (@Id, @Timestamp, @SnapshotJson);
        ";
        command.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@Timestamp", snapshot.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("@SnapshotJson", JsonSerializer.Serialize(snapshot));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<DiagnosticsSnapshot>> GetScanHistoryAsync()
    {
        var list = new List<DiagnosticsSnapshot>();
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT SnapshotJson FROM ScanResults ORDER BY Timestamp DESC LIMIT 50;";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            var snapshot = JsonSerializer.Deserialize<DiagnosticsSnapshot>(json);
            if (snapshot != null)
            {
                list.Add(snapshot);
            }
        }
        return list;
    }

    public async Task SaveSessionAsync(SessionRecord session)
    {
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO Sessions (Id, Timestamp, Description, SystemRestorePointCreated)
            VALUES (@Id, @Timestamp, @Description, @SystemRestorePointCreated);
        ";
        command.Parameters.AddWithValue("@Id", session.Id);
        command.Parameters.AddWithValue("@Timestamp", session.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("@Description", session.Description);
        command.Parameters.AddWithValue("@SystemRestorePointCreated", session.SystemRestorePointCreated ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveActionAsync(ActionRecord action)
    {
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO Actions (Id, SessionId, Timestamp, Module, ActionType, TargetName, Details, BeforeStateJson, IsReversible, IsUndone)
            VALUES (@Id, @SessionId, @Timestamp, @Module, @ActionType, @TargetName, @Details, @BeforeStateJson, @IsReversible, @IsUndone);
        ";
        command.Parameters.AddWithValue("@Id", action.Id);
        command.Parameters.AddWithValue("@SessionId", action.SessionId);
        command.Parameters.AddWithValue("@Timestamp", action.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("@Module", action.Module);
        command.Parameters.AddWithValue("@ActionType", action.ActionType);
        command.Parameters.AddWithValue("@TargetName", action.TargetName);
        command.Parameters.AddWithValue("@Details", action.Details ?? string.Empty);
        command.Parameters.AddWithValue("@BeforeStateJson", action.BeforeStateJson ?? string.Empty);
        command.Parameters.AddWithValue("@IsReversible", action.IsReversible ? 1 : 0);
        command.Parameters.AddWithValue("@IsUndone", action.IsUndone ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<SessionRecord>> GetAllSessionsAsync()
    {
        var sessionsMap = new Dictionary<string, SessionRecord>();
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var cmdSessions = connection.CreateCommand();
        cmdSessions.CommandText = "SELECT Id, Timestamp, Description, SystemRestorePointCreated FROM Sessions ORDER BY Timestamp DESC;";
        using (var reader = await cmdSessions.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var s = new SessionRecord
                {
                    Id = reader.GetString(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    Description = reader.GetString(2),
                    SystemRestorePointCreated = reader.GetInt32(3) == 1,
                    Actions = new List<ActionRecord>()
                };
                sessionsMap[s.Id] = s;
            }
        }

        var cmdActions = connection.CreateCommand();
        cmdActions.CommandText = "SELECT Id, SessionId, Timestamp, Module, ActionType, TargetName, Details, BeforeStateJson, IsReversible, IsUndone FROM Actions ORDER BY Timestamp ASC;";
        using (var reader = await cmdActions.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var action = new ActionRecord
                {
                    Id = reader.GetString(0),
                    SessionId = reader.GetString(1),
                    Timestamp = DateTime.Parse(reader.GetString(2)),
                    Module = reader.GetString(3),
                    ActionType = reader.GetString(4),
                    TargetName = reader.GetString(5),
                    Details = reader.GetString(6),
                    BeforeStateJson = reader.GetString(7),
                    IsReversible = reader.GetInt32(8) == 1,
                    IsUndone = reader.GetInt32(9) == 1
                };

                if (sessionsMap.TryGetValue(action.SessionId, out var session))
                {
                    session.Actions.Add(action);
                }
            }
        }

        return new List<SessionRecord>(sessionsMap.Values);
    }
}
