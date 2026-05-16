using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using EyeRest.Models;

namespace EyeRest.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ILogger<AnalyticsService> _logger;
        private readonly string _databasePath;
        private readonly string _connectionString;
        private readonly object _dbLock = new object();
        
        private int _currentSessionId = -1;
        private DateTime _sessionStartTime;
        
        // ENHANCED: Session activity tracking to exclude inactive time
        private DateTime _lastActiveTime;
        private TimeSpan _totalActiveTimeThisSession = TimeSpan.Zero;
        private TimeSpan _totalInactiveTimeThisSession = TimeSpan.Zero;
        private SessionState _currentSessionState = SessionState.Active;
        private DateTime _sessionPauseTime;
        private readonly object _sessionLock = new object();

        public AnalyticsService(ILogger<AnalyticsService> logger)
        {
            _logger = logger;
            
            // Create database directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var databaseDirectory = Path.Combine(appDataPath, "EyeRest", "Analytics");
            Directory.CreateDirectory(databaseDirectory);
            
            _databasePath = Path.Combine(databaseDirectory, "eyerest_analytics.db");
            _connectionString = $"Data Source={_databasePath};";
            
            _logger.LogInformation($"📊 Analytics database path: {_databasePath}");
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("📊 Initializing analytics database");
                
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    // Create tables
                    CreateTables(connection);
                    
                    // Create indexes for performance
                    CreateIndexes(connection);
                }
                
                _logger.LogInformation("✅ Analytics database initialized successfully");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize analytics database");
                throw;
            }
        }

        public async Task<bool> IsDatabaseInitializedAsync()
        {
            await Task.CompletedTask;
            try
            {
                if (!File.Exists(_databasePath))
                    return false;
                
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='UserSessions'";
                    var result = command.ExecuteScalar();
                    
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database initialization");
                return false;
            }
        }

        private void CreateTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            
            // Create UserSessions table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS UserSessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartTime DATETIME NOT NULL,
                    EndTime DATETIME,
                    TotalActiveTime INTEGER DEFAULT 0,
                    IdleTime INTEGER DEFAULT 0,
                    InactiveTime INTEGER DEFAULT 0,
                    PresenceChanges INTEGER DEFAULT 0,
                    SessionState TEXT DEFAULT 'Active',
                    LastActiveTime DATETIME NOT NULL
                )";
            command.ExecuteNonQuery();
            
            // Create RestEvents table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS RestEvents (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    EventType TEXT NOT NULL,
                    TriggeredAt DATETIME NOT NULL,
                    UserAction TEXT NOT NULL,
                    Duration INTEGER,
                    ConfiguredDuration INTEGER,
                    SessionId INTEGER,
                    TriggerSource TEXT NOT NULL DEFAULT 'Automatic',
                    FOREIGN KEY (SessionId) REFERENCES UserSessions (Id)
                )";
            command.ExecuteNonQuery();

            // Idempotent migration: add TriggerSource column to legacy databases
            command.CommandText = "PRAGMA table_info(RestEvents)";
            bool hasTriggerSource = false;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "TriggerSource", StringComparison.OrdinalIgnoreCase))
                    {
                        hasTriggerSource = true;
                        break;
                    }
                }
            }
            if (!hasTriggerSource)
            {
                command.CommandText = "ALTER TABLE RestEvents ADD COLUMN TriggerSource TEXT NOT NULL DEFAULT 'Automatic'";
                command.ExecuteNonQuery();
                _logger.LogInformation("📊 Migrated RestEvents: added TriggerSource column");
            }
            
            // Create PresenceEvents table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS PresenceEvents (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp DATETIME NOT NULL,
                    OldState TEXT NOT NULL,
                    NewState TEXT NOT NULL,
                    IdleDuration INTEGER,
                    SessionId INTEGER,
                    FOREIGN KEY (SessionId) REFERENCES UserSessions (Id)
                )";
            command.ExecuteNonQuery();
            
            // Create ResumeEvents table (MISSING TABLE)
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ResumeEvents (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp DATETIME NOT NULL,
                    Reason TEXT NOT NULL,
                    SessionId INTEGER,
                    FOREIGN KEY (SessionId) REFERENCES UserSessions (Id)
                )";
            command.ExecuteNonQuery();

            // Create PauseEvents table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS PauseEvents (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp DATETIME NOT NULL,
                    Reason TEXT NOT NULL,
                    SessionId INTEGER,
                    FOREIGN KEY (SessionId) REFERENCES UserSessions (Id)
                )";
            command.ExecuteNonQuery();

            // Create EventHistory table (JSON metadata column for extensibility)
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS EventHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp DATETIME NOT NULL,
                    EventType TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    Metadata TEXT DEFAULT '{}',
                    SessionId INTEGER,
                    FOREIGN KEY (SessionId) REFERENCES UserSessions (Id)
                )";
            command.ExecuteNonQuery();

            _logger.LogInformation("📊 Database tables created successfully");
        }

        private void CreateIndexes(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            
            // Create indexes for performance
            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_usersessions_starttime ON UserSessions (StartTime);
                CREATE INDEX IF NOT EXISTS idx_restevents_triggeredat ON RestEvents (TriggeredAt);
                CREATE INDEX IF NOT EXISTS idx_presenceevents_timestamp ON PresenceEvents (Timestamp);
                CREATE INDEX IF NOT EXISTS idx_resumeevents_timestamp ON ResumeEvents (Timestamp);
                CREATE INDEX IF NOT EXISTS idx_pauseevents_timestamp ON PauseEvents (Timestamp);
                CREATE INDEX IF NOT EXISTS idx_eventhistory_timestamp ON EventHistory (Timestamp);
                CREATE INDEX IF NOT EXISTS idx_eventhistory_eventtype ON EventHistory (EventType);
            ";
            command.ExecuteNonQuery();
            
            _logger.LogInformation("📊 Database indexes created successfully");
        }

        public async Task RecordSessionStartAsync()
        {
            try
            {
                var now = DateTime.Now;
                
                lock (_sessionLock)
                {
                    _sessionStartTime = now;
                    _lastActiveTime = now;
                    _totalActiveTimeThisSession = TimeSpan.Zero;
                    _totalInactiveTimeThisSession = TimeSpan.Zero;
                    _currentSessionState = SessionState.Active;
                    _sessionPauseTime = default;
                }
                
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO UserSessions (StartTime, TotalActiveTime, IdleTime, InactiveTime, PresenceChanges, SessionState, LastActiveTime)
                        VALUES (@startTime, 0, 0, 0, 0, @sessionState, @lastActiveTime);
                        SELECT last_insert_rowid();";
                    
                    command.Parameters.AddWithValue("@startTime", _sessionStartTime);
                    command.Parameters.AddWithValue("@sessionState", _currentSessionState.ToString());
                    command.Parameters.AddWithValue("@lastActiveTime", _lastActiveTime);
                    
                    _currentSessionId = Convert.ToInt32(command.ExecuteScalar());
                }
                
                _logger.LogInformation($"📊 Enhanced session started - ID: {_currentSessionId}, State: {_currentSessionState}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording session start");
            }
        }

        public async Task RecordSessionEndAsync()
        {
            try
            {
                if (_currentSessionId == -1) return;
                
                var now = DateTime.Now;
                var sessionDuration = now - _sessionStartTime;
                
                // Finalize active time calculation if session is currently active
                lock (_sessionLock)
                {
                    if (_currentSessionState == SessionState.Active)
                    {
                        var activeTime = now - _lastActiveTime;
                        _totalActiveTimeThisSession = _totalActiveTimeThisSession.Add(activeTime);
                        _logger.LogDebug($"📊 Final active period: {activeTime.TotalMinutes:F1} minutes");
                    }
                }
                
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        UPDATE UserSessions 
                        SET EndTime = @endTime,
                            TotalActiveTime = @totalActiveTime,
                            InactiveTime = @inactiveTime,
                            SessionState = @sessionState
                        WHERE Id = @sessionId";
                    
                    command.Parameters.AddWithValue("@endTime", now);
                    command.Parameters.AddWithValue("@totalActiveTime", (int)_totalActiveTimeThisSession.TotalMilliseconds);
                    command.Parameters.AddWithValue("@inactiveTime", (int)_totalInactiveTimeThisSession.TotalMilliseconds);
                    command.Parameters.AddWithValue("@sessionState", SessionState.Ended.ToString());
                    command.Parameters.AddWithValue("@sessionId", _currentSessionId);
                    
                    command.ExecuteNonQuery();
                }
                
                _logger.LogInformation($"📊 Enhanced session ended - ID: {_currentSessionId}, Total: {sessionDuration.TotalMinutes:F1}min, Active: {_totalActiveTimeThisSession.TotalMinutes:F1}min, Inactive: {_totalInactiveTimeThisSession.TotalMinutes:F1}min");
                _currentSessionId = -1;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording session end");
            }
        }

        public async Task RecordEyeRestEventAsync(RestEventType type, UserAction action, TimeSpan duration)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO RestEvents (EventType, TriggeredAt, UserAction, Duration, ConfiguredDuration)
                        VALUES (@eventType, @triggeredAt, @userAction, @duration, @configuredDuration)";
                    
                    command.Parameters.AddWithValue("@eventType", type.ToString());
                    command.Parameters.AddWithValue("@triggeredAt", DateTime.Now);
                    command.Parameters.AddWithValue("@userAction", action.ToString());
                    command.Parameters.AddWithValue("@duration", (int)duration.TotalMilliseconds);
                    command.Parameters.AddWithValue("@configuredDuration", type == RestEventType.EyeRest ? 20000 : 5000); // Default durations
                    
                    command.ExecuteNonQuery();
                }
                
                _logger.LogDebug($"📊 Recorded {type} event: {action}, Duration: {duration.TotalSeconds}s");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording rest event");
            }
        }

        public async Task RecordBreakEventAsync(RestEventType type, UserAction action, TimeSpan duration, BreakTriggerSource source = BreakTriggerSource.Automatic)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO RestEvents (EventType, TriggeredAt, UserAction, Duration, ConfiguredDuration, TriggerSource)
                        VALUES (@eventType, @triggeredAt, @userAction, @duration, @configuredDuration, @triggerSource)";

                    command.Parameters.AddWithValue("@eventType", type.ToString());
                    command.Parameters.AddWithValue("@triggeredAt", DateTime.Now);
                    command.Parameters.AddWithValue("@userAction", action.ToString());
                    command.Parameters.AddWithValue("@duration", (int)duration.TotalMilliseconds);
                    command.Parameters.AddWithValue("@configuredDuration", type == RestEventType.EyeRest ? 20000 : 5000);
                    command.Parameters.AddWithValue("@triggerSource", source.ToString());

                    command.ExecuteNonQuery();
                }

                _logger.LogDebug($"📊 Recorded {type} event: {action} ({source}), Duration: {duration.TotalSeconds}s");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording break event");
            }
        }

        public async Task<(int Automatic, int Manual)> GetBreakCountsBySourceAsync(int days)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-Math.Max(days, 0));
                int autoCount = 0, manualCount = 0;

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT TriggerSource, COUNT(*) FROM RestEvents
                        WHERE EventType = 'Break' AND TriggeredAt >= @cutoff
                        GROUP BY TriggerSource";
                    command.Parameters.AddWithValue("@cutoff", cutoff);

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var src = reader.GetString(0);
                        var count = reader.GetInt32(1);
                        if (string.Equals(src, "Manual", StringComparison.OrdinalIgnoreCase)) manualCount = count;
                        else autoCount += count;
                    }
                }

                await Task.CompletedTask;
                return (autoCount, manualCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying break counts by source");
                return (0, 0);
            }
        }

        /// <summary>
        /// NEW: Pause session activity tracking when user goes away or system sleeps
        /// </summary>
        public async Task PauseSessionAsync(UserPresenceState awayState, string reason = "")
        {
            try
            {
                if (_currentSessionId == -1) return;
                
                var now = DateTime.Now;
                
                lock (_sessionLock)
                {
                    // Only pause if currently active
                    if (_currentSessionState != SessionState.Active) return;
                    
                    // Calculate and accumulate active time up to this point
                    var activeTime = now - _lastActiveTime;
                    _totalActiveTimeThisSession = _totalActiveTimeThisSession.Add(activeTime);
                    
                    // Update session state
                    _currentSessionState = awayState switch
                    {
                        UserPresenceState.Away => SessionState.Away,
                        UserPresenceState.SystemSleep => SessionState.Sleep,
                        UserPresenceState.Idle => SessionState.Idle,
                        _ => SessionState.Paused
                    };
                    
                    _sessionPauseTime = now;
                    
                    _logger.LogInformation($"🔴 Session paused - ID: {_currentSessionId}, State: {_currentSessionState}, Active time this period: {activeTime.TotalMinutes:F1}min, Total active: {_totalActiveTimeThisSession.TotalMinutes:F1}min. Reason: {reason}");
                }
                
                // Update database with current state
                await UpdateSessionStateInDatabaseAsync(_currentSessionState, now);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing session");
            }
        }
        
        /// <summary>
        /// NEW: Resume session activity tracking when user returns
        /// </summary>
        public async Task ResumeSessionAsync(string reason = "")
        {
            try
            {
                if (_currentSessionId == -1) return;
                
                var now = DateTime.Now;
                
                lock (_sessionLock)
                {
                    // Only resume if currently paused/away/asleep
                    if (_currentSessionState == SessionState.Active) return;
                    
                    // Calculate inactive time
                    if (_sessionPauseTime != default)
                    {
                        var inactiveTime = now - _sessionPauseTime;
                        _totalInactiveTimeThisSession = _totalInactiveTimeThisSession.Add(inactiveTime);
                        
                        _logger.LogInformation($"🔵 Session resumed - ID: {_currentSessionId}, Was {_currentSessionState} for {inactiveTime.TotalMinutes:F1}min, Total inactive: {_totalInactiveTimeThisSession.TotalMinutes:F1}min. Reason: {reason}");
                    }
                    
                    // Resume active tracking
                    _currentSessionState = SessionState.Active;
                    _lastActiveTime = now;
                    _sessionPauseTime = default;
                }
                
                // Update database with current state
                await UpdateSessionStateInDatabaseAsync(_currentSessionState, now);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming session");
            }
        }
        
        /// <summary>
        /// NEW: Validate session activity tracking integrity
        /// </summary>
        public SessionActivityValidationResult ValidateSessionTracking()
        {
            try
            {
                var now = DateTime.Now;
                var validation = new SessionActivityValidationResult
                {
                    ValidationTime = now,
                    SessionId = _currentSessionId,
                    IsValid = true,
                    ValidationMessages = new List<string>()
                };
                
                if (_currentSessionId == -1)
                {
                    validation.ValidationMessages.Add("✅ No active session - validation skipped");
                    return validation;
                }
                
                var sessionDuration = now - _sessionStartTime;
                var totalTrackedTime = _totalActiveTimeThisSession + _totalInactiveTimeThisSession;
                
                // Add current period to tracked time based on state
                lock (_sessionLock)
                {
                    if (_currentSessionState == SessionState.Active && _lastActiveTime != default)
                    {
                        var currentActivePeriod = now - _lastActiveTime;
                        totalTrackedTime = totalTrackedTime.Add(currentActivePeriod);
                    }
                    else if (_sessionPauseTime != default)
                    {
                        var currentInactivePeriod = now - _sessionPauseTime;
                        totalTrackedTime = totalTrackedTime.Add(currentInactivePeriod);
                    }
                }
                
                // Validation checks
                var timeDifference = Math.Abs((sessionDuration - totalTrackedTime).TotalSeconds);
                if (timeDifference > 30) // Allow 30-second tolerance
                {
                    validation.IsValid = false;
                    validation.ValidationMessages.Add($"❌ Time tracking mismatch: Session={sessionDuration.TotalMinutes:F1}min, Tracked={totalTrackedTime.TotalMinutes:F1}min, Diff={timeDifference:F1}s");
                }
                else
                {
                    validation.ValidationMessages.Add($"✅ Time tracking accurate within tolerance: {timeDifference:F1}s difference");
                }
                
                // State consistency checks
                if (_currentSessionState == SessionState.Active && _lastActiveTime == default)
                {
                    validation.IsValid = false;
                    validation.ValidationMessages.Add("❌ Inconsistent state: Active session with no lastActiveTime");
                }
                
                if (_currentSessionState != SessionState.Active && _sessionPauseTime == default)
                {
                    validation.ValidationMessages.Add($"⚠️ Warning: {_currentSessionState} state with no pauseTime (may be recent state change)");
                }
                
                // Add summary
                var activityRate = sessionDuration.TotalMinutes > 0 
                    ? _totalActiveTimeThisSession.TotalMinutes / sessionDuration.TotalMinutes 
                    : 0.0;
                
                validation.ValidationMessages.Add($"📊 Session {_currentSessionId}: {_currentSessionState} | Active: {_totalActiveTimeThisSession.TotalMinutes:F1}min ({activityRate:P0}) | Inactive: {_totalInactiveTimeThisSession.TotalMinutes:F1}min");
                
                return validation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session tracking");
                return new SessionActivityValidationResult
                {
                    ValidationTime = DateTime.Now,
                    SessionId = _currentSessionId,
                    IsValid = false,
                    ValidationMessages = new List<string> { $"❌ Validation error: {ex.Message}" }
                };
            }
        }
        
        /// <summary>
        /// NEW: Get current session activity metrics in real-time
        /// </summary>
        public SessionActivityMetrics GetCurrentSessionMetrics()
        {
            try
            {
                if (_currentSessionId == -1)
                {
                    return new SessionActivityMetrics
                    {
                        SessionId = -1,
                        SessionStartTime = default,
                        CurrentState = SessionState.Ended,
                        TotalSessionTime = TimeSpan.Zero,
                        ActiveTime = TimeSpan.Zero,
                        InactiveTime = TimeSpan.Zero,
                        ActivityRate = 0.0
                    };
                }
                
                var now = DateTime.Now;
                var totalSessionTime = now - _sessionStartTime;
                var currentActiveTime = _totalActiveTimeThisSession;
                var currentInactiveTime = _totalInactiveTimeThisSession;
                
                lock (_sessionLock)
                {
                    // Add current period to totals based on state
                    if (_currentSessionState == SessionState.Active)
                    {
                        var currentActivePeriod = now - _lastActiveTime;
                        currentActiveTime = currentActiveTime.Add(currentActivePeriod);
                    }
                    else if (_sessionPauseTime != default)
                    {
                        var currentInactivePeriod = now - _sessionPauseTime;
                        currentInactiveTime = currentInactiveTime.Add(currentInactivePeriod);
                    }
                }
                
                var activityRate = totalSessionTime.TotalMinutes > 0 
                    ? currentActiveTime.TotalMinutes / totalSessionTime.TotalMinutes 
                    : 0.0;
                
                return new SessionActivityMetrics
                {
                    SessionId = _currentSessionId,
                    SessionStartTime = _sessionStartTime,
                    CurrentState = _currentSessionState,
                    TotalSessionTime = totalSessionTime,
                    ActiveTime = currentActiveTime,
                    InactiveTime = currentInactiveTime,
                    ActivityRate = activityRate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current session metrics");
                return new SessionActivityMetrics { SessionId = -1, CurrentState = SessionState.Error };
            }
        }
        
        /// <summary>
        /// NEW: Update session state in database
        /// </summary>
        private async Task UpdateSessionStateInDatabaseAsync(SessionState state, DateTime timestamp)
        {
            try
            {
                if (_currentSessionId == -1) return;
                
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        UPDATE UserSessions 
                        SET SessionState = @sessionState,
                            LastActiveTime = @lastActiveTime,
                            TotalActiveTime = @totalActiveTime,
                            InactiveTime = @inactiveTime
                        WHERE Id = @sessionId";
                    
                    command.Parameters.AddWithValue("@sessionState", state.ToString());
                    command.Parameters.AddWithValue("@lastActiveTime", timestamp);
                    command.Parameters.AddWithValue("@totalActiveTime", (int)_totalActiveTimeThisSession.TotalMilliseconds);
                    command.Parameters.AddWithValue("@inactiveTime", (int)_totalInactiveTimeThisSession.TotalMilliseconds);
                    command.Parameters.AddWithValue("@sessionId", _currentSessionId);
                    
                    command.ExecuteNonQuery();
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session state in database");
            }
        }
        
        public async Task RecordPresenceChangeAsync(UserPresenceState oldState, UserPresenceState newState, TimeSpan idleDuration)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO PresenceEvents (Timestamp, OldState, NewState, IdleDuration)
                        VALUES (@timestamp, @oldState, @newState, @idleDuration)";
                    
                    command.Parameters.AddWithValue("@timestamp", DateTime.Now);
                    command.Parameters.AddWithValue("@oldState", oldState.ToString());
                    command.Parameters.AddWithValue("@newState", newState.ToString());
                    command.Parameters.AddWithValue("@idleDuration", (int)idleDuration.TotalMilliseconds);
                    
                    command.ExecuteNonQuery();
                    
                    // Update session presence changes count
                    if (_currentSessionId != -1)
                    {
                        using var updateCommand = connection.CreateCommand();
                        updateCommand.CommandText = @"
                            UPDATE UserSessions 
                            SET PresenceChanges = PresenceChanges + 1
                            WHERE Id = @sessionId";
                        updateCommand.Parameters.AddWithValue("@sessionId", _currentSessionId);
                        updateCommand.ExecuteNonQuery();
                    }
                }
                
                _logger.LogDebug($"📊 Recorded presence change: {oldState} → {newState}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording presence change");
            }
        }

        public async Task RecordMeetingEventAsync(MeetingApplication meeting, bool timersPaused)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR REPLACE INTO MeetingEvents (StartTime, EndTime, ApplicationName, MeetingType, TimersPaused)
                        VALUES (@startTime, @endTime, @applicationName, @meetingType, @timersPaused)";
                    
                    command.Parameters.AddWithValue("@startTime", meeting.StartTime);
                    command.Parameters.AddWithValue("@endTime", meeting.EndTime);
                    command.Parameters.AddWithValue("@applicationName", meeting.ProcessName);
                    command.Parameters.AddWithValue("@meetingType", meeting.Type.ToString());
                    command.Parameters.AddWithValue("@timersPaused", timersPaused);
                    
                    command.ExecuteNonQuery();
                }
                
                _logger.LogDebug($"📊 Recorded meeting event: {meeting.Type} - {meeting.ProcessName}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording meeting event");
            }
        }

        public async Task<HealthMetrics> GetHealthMetricsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var metrics = new HealthMetrics
                {
                    PeriodStart = startDate,
                    PeriodEnd = endDate
                };
                
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    // Get break statistics
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT 
                            COUNT(*) as Total,
                            SUM(CASE WHEN UserAction = 'Completed' THEN 1 ELSE 0 END) as Completed,
                            SUM(CASE WHEN UserAction = 'Skipped' THEN 1 ELSE 0 END) as Skipped,
                            SUM(CASE WHEN UserAction IN ('Delayed1Min', 'Delayed5Min') THEN 1 ELSE 0 END) as Delayed,
                            AVG(CASE WHEN UserAction = 'Completed' THEN Duration ELSE NULL END) as AvgDuration
                        FROM RestEvents 
                        WHERE EventType = @eventType 
                        AND TriggeredAt BETWEEN @startDate AND @endDate";
                    
                    // Break metrics
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@eventType", "Break");
                    command.Parameters.AddWithValue("@startDate", startDate);
                    command.Parameters.AddWithValue("@endDate", endDate);
                    
                    using var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        metrics.BreaksCompleted = reader.GetInt32(1);       // Completed
                        metrics.BreaksSkipped = reader.GetInt32(2);         // Skipped
                        metrics.BreaksDelayed = reader.GetInt32(3);         // Delayed (informational)

                        // A break opportunity resolves to exactly one Completed or Skipped event.
                        // Delayed events are intermediate states that eventually become one of those,
                        // so they must not be counted as separate opportunities or the compliance
                        // rate is permanently suppressed for anyone who ever uses "snooze".
                        metrics.TotalBreaksDue = metrics.BreaksCompleted + metrics.BreaksSkipped;

                        if (!reader.IsDBNull(4))
                        {
                            metrics.AverageBreakDuration = TimeSpan.FromMilliseconds(reader.GetDouble(4)); // AvgDuration
                        }
                    }
                    reader.Close();
                    
                    // Eye rest metrics
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@eventType", "EyeRest");
                    command.Parameters.AddWithValue("@startDate", startDate);
                    command.Parameters.AddWithValue("@endDate", endDate);
                    
                    using var eyeReader = command.ExecuteReader();
                    if (eyeReader.Read())
                    {
                        metrics.EyeRestsCompleted = eyeReader.GetInt32(0);  // Completed
                        metrics.EyeRestsSkipped = eyeReader.GetInt32(1);    // Skipped
                    }
                    eyeReader.Close();
                    
                    // Calculate compliance rate
                    if (metrics.TotalBreaksDue > 0)
                    {
                        metrics.ComplianceRate = (double)metrics.BreaksCompleted / metrics.TotalBreaksDue;
                    }
                    
                    // Get total active time from sessions
                    using var sessionCommand = connection.CreateCommand();
                    sessionCommand.CommandText = @"
                        SELECT SUM(TotalActiveTime) as TotalActive
                        FROM UserSessions
                        WHERE StartTime BETWEEN @startDate AND @endDate";
                    
                    sessionCommand.Parameters.AddWithValue("@startDate", startDate);
                    sessionCommand.Parameters.AddWithValue("@endDate", endDate);
                    
                    var activeTimeResult = sessionCommand.ExecuteScalar();
                    if (activeTimeResult != DBNull.Value)
                    {
                        metrics.TotalActiveTime = TimeSpan.FromMilliseconds(Convert.ToDouble(activeTimeResult));
                    }
                    
                }
                
                // Get daily breakdown outside of lock
                metrics.DailyBreakdown = await GetDailyMetricsAsync(startDate, endDate);
                
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health metrics");
                return new HealthMetrics { PeriodStart = startDate, PeriodEnd = endDate };
            }
        }

        public async Task<ComplianceReport> GenerateComplianceReportAsync(int days = 30)
        {
            var endDate = DateTime.Now.Date;
            var startDate = endDate.AddDays(-days);
            
            try
            {
                var healthMetrics = await GetHealthMetricsAsync(startDate, endDate);
                
                var report = new ComplianceReport
                {
                    GeneratedAt = DateTime.Now,
                    DaysAnalyzed = days,
                    OverallComplianceRate = healthMetrics.ComplianceRate,
                    TotalActiveTime = healthMetrics.TotalActiveTime,
                    TotalBreakTime = TimeSpan.FromMilliseconds(
                        healthMetrics.BreaksCompleted * healthMetrics.AverageBreakDuration.TotalMilliseconds)
                };
                
                // Calculate individual compliance rates
                var totalEyeRests = healthMetrics.EyeRestsCompleted + healthMetrics.EyeRestsSkipped;
                if (totalEyeRests > 0)
                {
                    report.EyeRestComplianceRate = (double)healthMetrics.EyeRestsCompleted / totalEyeRests;
                }
                
                report.BreakComplianceRate = healthMetrics.ComplianceRate;
                
                // Generate trends
                report.Trends = GenerateComplianceTrends(healthMetrics.DailyBreakdown);
                
                // Generate recommendations
                report.Recommendations = GenerateRecommendations(report);
                
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance report");
                return new ComplianceReport { GeneratedAt = DateTime.Now, DaysAnalyzed = days };
            }
        }

        public async Task<List<DailyMetric>> GetDailyMetricsAsync(DateTime startDate, DateTime endDate)
        {
            await Task.CompletedTask;
            var dailyMetrics = new List<DailyMetric>();

            try
            {
                // Pre-create empty metrics for every day in range
                var metricsByDate = new Dictionary<DateTime, DailyMetric>();
                var currentDate = startDate.Date;
                while (currentDate <= endDate.Date)
                {
                    var metric = new DailyMetric { Date = currentDate };
                    metricsByDate[currentDate] = metric;
                    dailyMetrics.Add(metric);
                    currentDate = currentDate.AddDays(1);
                }

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    // Single query: group by date, EventType, UserAction
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT
                            date(TriggeredAt) as EventDate,
                            EventType,
                            UserAction,
                            COUNT(*) as Count,
                            SUM(Duration) as TotalDuration
                        FROM RestEvents
                        WHERE TriggeredAt >= @startDate AND TriggeredAt < @endDate
                        GROUP BY date(TriggeredAt), EventType, UserAction
                        ORDER BY EventDate";

                    command.Parameters.AddWithValue("@startDate", startDate.Date);
                    command.Parameters.AddWithValue("@endDate", endDate.Date.AddDays(1));

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var eventDateStr = reader.GetString(0);  // EventDate (date string)
                        var eventType = reader.GetString(1);     // EventType
                        var userAction = reader.GetString(2);    // UserAction
                        var count = reader.GetInt32(3);          // Count
                        var duration = reader.IsDBNull(4) ? 0L : reader.GetInt64(4); // TotalDuration

                        var eventDate = DateTime.Parse(eventDateStr).Date;

                        if (!metricsByDate.TryGetValue(eventDate, out var metric))
                            continue;

                        if (eventType == "Break")
                        {
                            switch (userAction)
                            {
                                case "Completed":
                                    metric.BreaksCompleted = count;
                                    metric.TotalBreakTime = TimeSpan.FromMilliseconds(duration);
                                    break;
                                case "Skipped":
                                    metric.BreaksSkipped = count;
                                    break;
                                case "Delayed1Min":
                                case "Delayed5Min":
                                    metric.BreaksDelayed += count;
                                    break;
                            }
                        }
                        else if (eventType == "EyeRest")
                        {
                            switch (userAction)
                            {
                                case "Completed":
                                    metric.EyeRestsCompleted = count;
                                    break;
                                case "Skipped":
                                    metric.EyeRestsSkipped = count;
                                    break;
                            }
                        }
                    }
                }

                // Calculate derived fields for all metrics
                foreach (var metric in dailyMetrics)
                {
                    // BreaksDue counts opportunities, not events. A "Delayed" row is an
                    // intermediate state of a single opportunity — the resolving
                    // Completed/Skipped row is what makes it a counted opportunity.
                    // Counting delays here would penalise users who ever snooze a break.
                    metric.BreaksDue = metric.BreaksCompleted + metric.BreaksSkipped;
                    metric.EyeRestsDue = metric.EyeRestsCompleted + metric.EyeRestsSkipped;

                    if (metric.BreaksDue > 0)
                    {
                        metric.ComplianceRate = (double)metric.BreaksCompleted / metric.BreaksDue;
                    }
                }

                return dailyMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily metrics");
                return dailyMetrics;
            }
        }

        public async Task<List<SessionSummary>> GetSessionSummariesAsync(DateTime startDate, DateTime endDate)
        {
            await Task.CompletedTask;
            var sessions = new List<SessionSummary>();
            
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT StartTime, EndTime, TotalActiveTime, IdleTime, PresenceChanges
                        FROM UserSessions
                        WHERE StartTime BETWEEN @startDate AND @endDate
                        ORDER BY StartTime DESC";
                    
                    command.Parameters.AddWithValue("@startDate", startDate);
                    command.Parameters.AddWithValue("@endDate", endDate);
                    
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var session = new SessionSummary
                        {
                            StartTime = reader.GetDateTime(0),                                              // StartTime
                            ActiveTime = TimeSpan.FromMilliseconds(reader.GetInt32(2)),                    // TotalActiveTime
                            IdleTime = TimeSpan.FromMilliseconds(reader.GetInt32(3)),                      // IdleTime
                            PresenceChanges = reader.GetInt32(4)                                           // PresenceChanges
                        };
                        
                        if (!reader.IsDBNull(1))
                        {
                            session.EndTime = reader.GetDateTime(1);                                       // EndTime
                            session.Duration = session.EndTime - session.StartTime;
                        }
                        
                        sessions.Add(session);
                    }
                }
                
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session summaries");
                return sessions;
            }
        }

        public async Task<List<MeetingStats>> GetMeetingStatsAsync(DateTime startDate, DateTime endDate)
        {
            await Task.CompletedTask;
            var stats = new List<MeetingStats>();

            try
            {
                // Pre-create empty stats for every day in range
                var statsByDate = new Dictionary<DateTime, MeetingStats>();
                var currentDate = startDate.Date;
                while (currentDate <= endDate.Date)
                {
                    var stat = new MeetingStats { Date = currentDate };
                    statsByDate[currentDate] = stat;
                    stats.Add(stat);
                    currentDate = currentDate.AddDays(1);
                }

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    // Single query: group by date and MeetingType
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT
                            date(StartTime) as EventDate,
                            MeetingType,
                            COUNT(*) as Count,
                            SUM(JULIANDAY(COALESCE(EndTime, datetime('now'))) - JULIANDAY(StartTime)) * 24 * 60 as TotalMinutes,
                            SUM(CASE WHEN TimersPaused = 1 THEN 1 ELSE 0 END) as PausedCount
                        FROM MeetingEvents
                        WHERE StartTime >= @startDate AND StartTime < @endDate
                        GROUP BY date(StartTime), MeetingType
                        ORDER BY EventDate";

                    command.Parameters.AddWithValue("@startDate", startDate.Date);
                    command.Parameters.AddWithValue("@endDate", endDate.Date.AddDays(1));

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var eventDateStr = reader.GetString(0);                              // EventDate
                        var meetingType = Enum.Parse<MeetingType>(reader.GetString(1));       // MeetingType
                        var count = reader.GetInt32(2);                                      // Count
                        var minutes = reader.GetDouble(3);                                   // TotalMinutes
                        var pausedCount = reader.GetInt32(4);                                // PausedCount

                        var eventDate = DateTime.Parse(eventDateStr).Date;

                        if (!statsByDate.TryGetValue(eventDate, out var stat))
                            continue;

                        stat.MeetingsByType[meetingType] = count;
                        stat.TotalMeetings += count;
                        stat.TotalMeetingTime = stat.TotalMeetingTime.Add(TimeSpan.FromMinutes(minutes));
                        stat.TimersPausedCount += pausedCount;
                    }
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting meeting stats");
                return stats;
            }
        }

        public async Task<long> GetDatabaseSizeAsync()
        {
            await Task.CompletedTask;
            try
            {
                var fileInfo = new FileInfo(_databasePath);
                return fileInfo.Exists ? fileInfo.Length : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database size");
                return 0;
            }
        }

        public string GetDatabasePath()
        {
            return _databasePath;
        }

        public async Task<List<WeeklyMetrics>> GetWeeklyMetricsAsync(DateTime startDate, DateTime endDate, List<DailyMetric>? prefetchedDailyMetrics = null)
        {
            var weeklyMetrics = new List<WeeklyMetrics>();

            try
            {
                // Fetch daily metrics OUTSIDE the lock to avoid deadlock
                var dailyMetrics = prefetchedDailyMetrics ?? await GetDailyMetricsAsync(startDate, endDate);

                // Group by week
                var weeklyGroups = dailyMetrics
                    .GroupBy(d => new {
                        Year = d.Date.Year,
                        Week = GetWeekOfYear(d.Date)
                    })
                    .OrderByDescending(g => g.Key.Year)
                    .ThenByDescending(g => g.Key.Week);

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    foreach (var weekGroup in weeklyGroups)
                    {
                        var weekData = weekGroup.ToList();
                        var firstDay = weekData.Min(d => d.Date);
                        var lastDay = weekData.Max(d => d.Date);

                        var weekMetric = new WeeklyMetrics
                        {
                            WeekStartDate = firstDay,
                            WeekEndDate = lastDay,
                            WeekNumber = weekGroup.Key.Week,
                            Year = weekGroup.Key.Year,
                            DaysActive = weekData.Count(d => d.BreaksDue > 0),
                            TotalBreaks = weekData.Sum(d => d.BreaksDue),
                            CompletedBreaks = weekData.Sum(d => d.BreaksCompleted),
                            SkippedBreaks = weekData.Sum(d => d.BreaksSkipped),
                            DelayedBreaks = weekData.Sum(d => d.BreaksDelayed),
                            TotalEyeRests = weekData.Sum(d => d.EyeRestsDue),
                            CompletedEyeRests = weekData.Sum(d => d.EyeRestsCompleted),
                            SkippedEyeRests = weekData.Sum(d => d.EyeRestsSkipped),
                            TotalBreakTime = TimeSpan.FromTicks(weekData.Sum(d => d.TotalBreakTime.Ticks))
                        };

                        // Calculate compliance rate
                        if (weekMetric.TotalBreaks > 0)
                        {
                            weekMetric.ComplianceRate = (double)weekMetric.CompletedBreaks / weekMetric.TotalBreaks;
                        }

                        // Calculate average break time
                        if (weekMetric.CompletedBreaks > 0)
                        {
                            weekMetric.AverageBreakTime = TimeSpan.FromTicks(weekMetric.TotalBreakTime.Ticks / weekMetric.CompletedBreaks);
                        }

                        // Get total active time for the week
                        using var command = connection.CreateCommand();
                        command.CommandText = @"
                            SELECT SUM(TotalActiveTime) as TotalActive
                            FROM UserSessions
                            WHERE StartTime >= @startDate AND StartTime <= @endDate";

                        command.Parameters.AddWithValue("@startDate", firstDay);
                        command.Parameters.AddWithValue("@endDate", lastDay.AddDays(1));

                        var activeTimeResult = command.ExecuteScalar();
                        if (activeTimeResult != DBNull.Value)
                        {
                            weekMetric.TotalActiveTime = TimeSpan.FromMilliseconds(Convert.ToDouble(activeTimeResult));
                        }

                        weeklyMetrics.Add(weekMetric);
                    }
                }

                return weeklyMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weekly metrics");
                return weeklyMetrics;
            }
        }

        public async Task<List<MonthlyMetrics>> GetMonthlyMetricsAsync(DateTime startDate, DateTime endDate, List<DailyMetric>? prefetchedDailyMetrics = null)
        {
            var monthlyMetrics = new List<MonthlyMetrics>();

            try
            {
                // Fetch daily metrics OUTSIDE the lock to avoid deadlock
                var dailyMetrics = prefetchedDailyMetrics ?? await GetDailyMetricsAsync(startDate, endDate);

                // Group by month
                var monthlyGroups = dailyMetrics
                    .GroupBy(d => new {
                        Year = d.Date.Year,
                        Month = d.Date.Month
                    })
                    .OrderByDescending(g => g.Key.Year)
                    .ThenByDescending(g => g.Key.Month);

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    foreach (var monthGroup in monthlyGroups)
                    {
                        var monthData = monthGroup.ToList();
                        var firstDay = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1);
                        var lastDay = firstDay.AddMonths(1).AddDays(-1);

                        var monthMetric = new MonthlyMetrics
                        {
                            MonthStartDate = firstDay,
                            MonthEndDate = lastDay,
                            Month = monthGroup.Key.Month,
                            Year = monthGroup.Key.Year,
                            DaysActive = monthData.Count(d => d.BreaksDue > 0),
                            TotalBreaks = monthData.Sum(d => d.BreaksDue),
                            CompletedBreaks = monthData.Sum(d => d.BreaksCompleted),
                            SkippedBreaks = monthData.Sum(d => d.BreaksSkipped),
                            DelayedBreaks = monthData.Sum(d => d.BreaksDelayed),
                            TotalEyeRests = monthData.Sum(d => d.EyeRestsDue),
                            CompletedEyeRests = monthData.Sum(d => d.EyeRestsCompleted),
                            SkippedEyeRests = monthData.Sum(d => d.EyeRestsSkipped),
                            TotalBreakTime = TimeSpan.FromTicks(monthData.Sum(d => d.TotalBreakTime.Ticks)),
                            WeeksActive = monthData.GroupBy(d => GetWeekOfYear(d.Date)).Count()
                        };

                        // Calculate compliance rate
                        if (monthMetric.TotalBreaks > 0)
                        {
                            monthMetric.ComplianceRate = (double)monthMetric.CompletedBreaks / monthMetric.TotalBreaks;
                        }

                        // Calculate average break time
                        if (monthMetric.CompletedBreaks > 0)
                        {
                            monthMetric.AverageBreakTime = TimeSpan.FromTicks(monthMetric.TotalBreakTime.Ticks / monthMetric.CompletedBreaks);
                        }

                        // Get total active time for the month
                        using var command = connection.CreateCommand();
                        command.CommandText = @"
                            SELECT SUM(TotalActiveTime) as TotalActive
                            FROM UserSessions
                            WHERE StartTime >= @startDate AND StartTime < @endDate";

                        command.Parameters.AddWithValue("@startDate", firstDay);
                        command.Parameters.AddWithValue("@endDate", firstDay.AddMonths(1));

                        var activeTimeResult = command.ExecuteScalar();
                        if (activeTimeResult != DBNull.Value)
                        {
                            monthMetric.TotalActiveTime = TimeSpan.FromMilliseconds(Convert.ToDouble(activeTimeResult));
                        }

                        monthlyMetrics.Add(monthMetric);
                    }
                }

                return monthlyMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monthly metrics");
                return monthlyMetrics;
            }
        }

        public async Task CleanupOldDataAsync(DateTime cutoffDate)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var transaction = connection.BeginTransaction();
                    
                    try
                    {
                        // Delete old records from each table
                        var tables = new[]
                        {
                            ("RestEvents", "TriggeredAt"),
                            ("PresenceEvents", "Timestamp"),
                            ("MeetingEvents", "StartTime"),
                            ("UserSessions", "StartTime")
                        };
                        
                        var totalDeleted = 0;
                        
                        foreach (var (table, dateColumn) in tables)
                        {
                            using var command = connection.CreateCommand();
                            command.CommandText = $"DELETE FROM {table} WHERE {dateColumn} < @cutoffDate";
                            command.Parameters.AddWithValue("@cutoffDate", cutoffDate);
                            
                            var deleted = command.ExecuteNonQuery();
                            totalDeleted += deleted;
                            _logger.LogInformation($"📊 Deleted {deleted} old records from {table}");
                        }
                        
                        // Vacuum database to reclaim space
                        using var vacuumCommand = connection.CreateCommand();
                        vacuumCommand.CommandText = "VACUUM";
                        vacuumCommand.ExecuteNonQuery();
                        
                        transaction.Commit();
                        
                        _logger.LogInformation($"📊 Cleanup completed - removed {totalDeleted} total records older than {cutoffDate:yyyy-MM-dd}");
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old data");
                throw;
            }
        }

        public async Task<string> ExportDataAsync(ExportFormat format, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.Now.AddDays(-30);
                var end = endDate ?? DateTime.Now;
                
                var healthMetrics = await GetHealthMetricsAsync(start, end);
                var dailyMetrics = await GetDailyMetricsAsync(start, end);
                var weeklyMetrics = await GetWeeklyMetricsAsync(start, end);
                var monthlyMetrics = await GetMonthlyMetricsAsync(start, end);
                var sessions = await GetSessionSummariesAsync(start, end);
                var meetings = await GetMeetingStatsAsync(start, end);
                
                return format switch
                {
                    ExportFormat.Json => ExportToJson(healthMetrics, dailyMetrics, weeklyMetrics, monthlyMetrics, sessions, meetings),
                    ExportFormat.Csv => ExportToCsv(dailyMetrics, weeklyMetrics, monthlyMetrics),
                    ExportFormat.Html => ExportToHtml(healthMetrics, dailyMetrics, weeklyMetrics, monthlyMetrics),
                    _ => throw new ArgumentException($"Unsupported export format: {format}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data");
                throw;
            }
        }

        public async Task ImportDataAsync(string filePath)
        {
            // Implementation for importing data from backup files
            // This would be used for data recovery or migration scenarios
            await Task.CompletedTask;
            throw new NotImplementedException("Data import functionality not yet implemented");
        }

        public async Task DeleteAllDataAsync()
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    using var transaction = connection.BeginTransaction();
                    
                    try
                    {
                        var tables = new[] { "RestEvents", "PresenceEvents", "MeetingEvents", "EventHistory", "UserSessions" };
                        
                        foreach (var table in tables)
                        {
                            using var command = connection.CreateCommand();
                            command.CommandText = $"DELETE FROM {table}";
                            command.ExecuteNonQuery();
                        }
                        
                        // Reset auto-increment counters
                        using var resetCommand = connection.CreateCommand();
                        resetCommand.CommandText = "DELETE FROM sqlite_sequence";
                        resetCommand.ExecuteNonQuery();
                        
                        // Vacuum to reclaim space
                        using var vacuumCommand = connection.CreateCommand();
                        vacuumCommand.CommandText = "VACUUM";
                        vacuumCommand.ExecuteNonQuery();
                        
                        transaction.Commit();
                        
                        _logger.LogInformation("📊 All analytics data deleted successfully");
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all data");
                throw;
            }
        }

        private List<ComplianceTrend> GenerateComplianceTrends(List<DailyMetric> dailyMetrics)
        {
            var trends = new List<ComplianceTrend>();
            
            for (int i = 0; i < dailyMetrics.Count; i++)
            {
                var trend = new ComplianceTrend
                {
                    Date = dailyMetrics[i].Date,
                    ComplianceRate = dailyMetrics[i].ComplianceRate
                };
                
                if (i > 0)
                {
                    var previousRate = dailyMetrics[i - 1].ComplianceRate;
                    var difference = trend.ComplianceRate - previousRate;
                    
                    trend.Direction = difference switch
                    {
                        > 0.05 => TrendDirection.Up,
                        < -0.05 => TrendDirection.Down,
                        _ => TrendDirection.Stable
                    };
                }
                else
                {
                    trend.Direction = TrendDirection.Stable;
                }
                
                trends.Add(trend);
            }
            
            return trends;
        }

        private List<string> GenerateRecommendations(ComplianceReport report)
        {
            var recommendations = new List<string>();
            
            // Check if this is a new user with minimal data
            var hasMinimalData = report.TotalActiveTime.TotalHours < 2 || report.DaysAnalyzed < 3;
            
            if (hasMinimalData)
            {
                recommendations.Add("Welcome to your Eye-rest journey! Keep using the app to build healthy break habits.");
                recommendations.Add("Try to take breaks when prompted - your eyes will appreciate the rest.");
                recommendations.Add("Consider adjusting break timing in settings to match your work schedule.");
                return recommendations;
            }
            
            if (report.OverallComplianceRate < 0.7)
            {
                recommendations.Add("Consider enabling auto-pause during meetings to improve compliance");
                recommendations.Add("Review break timing settings to better match your work schedule");
            }
            
            if (report.EyeRestComplianceRate < 0.8)
            {
                recommendations.Add("Eye rest compliance could improve - consider reducing eye rest duration if it feels too long");
            }
            
            if (report.BreakComplianceRate < 0.6)
            {
                recommendations.Add("Try taking more breaks to build a healthy routine - consider shorter break intervals to start");
            }
            
            if (report.TotalActiveTime.TotalHours > 8)
            {
                recommendations.Add("Long working sessions detected - consider taking more frequent breaks to protect your eye health");
            }
            
            return recommendations;
        }

        private string ExportToJson(HealthMetrics health, List<DailyMetric> daily, List<WeeklyMetrics> weekly, List<MonthlyMetrics> monthly, List<SessionSummary> sessions, List<MeetingStats> meetings)
        {
            var exportData = new
            {
                ExportedAt = DateTime.Now,
                ExportVersion = "2.0",
                DataPrivacyNotice = "This data is exported from your local Eye-rest application. No data is sent to external servers.",
                PeriodStart = health.PeriodStart,
                PeriodEnd = health.PeriodEnd,
                HealthMetrics = health,
                DailyMetrics = daily,
                WeeklyMetrics = weekly,
                MonthlyMetrics = monthly,
                Sessions = sessions,
                MeetingStats = meetings
            };
            
            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        }

        private string ExportToCsv(List<DailyMetric> dailyMetrics, List<WeeklyMetrics> weeklyMetrics, List<MonthlyMetrics> monthlyMetrics)
        {
            var csv = new StringBuilder();
            
            // Add header with data privacy notice
            csv.AppendLine("# Eye-rest Analytics Export");
            csv.AppendLine("# Generated on: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            csv.AppendLine("# Data Privacy: All data is stored locally and never sent to external servers");
            csv.AppendLine("");
            
            // Daily metrics section
            csv.AppendLine("## DAILY METRICS");
            csv.AppendLine("Date,BreaksDue,BreaksCompleted,BreaksSkipped,BreaksDelayed,EyeRestsDue,EyeRestsCompleted,EyeRestsSkipped,ComplianceRate,TotalActiveTime,TotalBreakTime");
            
            foreach (var metric in dailyMetrics.OrderBy(d => d.Date))
            {
                csv.AppendLine($"{metric.Date:yyyy-MM-dd},{metric.BreaksDue},{metric.BreaksCompleted},{metric.BreaksSkipped},{metric.BreaksDelayed},{metric.EyeRestsDue},{metric.EyeRestsCompleted},{metric.EyeRestsSkipped},{metric.ComplianceRate:F3},{metric.TotalActiveTime.TotalMinutes:F1},{metric.TotalBreakTime.TotalMinutes:F1}");
            }
            
            csv.AppendLine("");
            
            // Weekly metrics section
            csv.AppendLine("## WEEKLY METRICS");
            csv.AppendLine("WeekStart,WeekEnd,WeekNumber,Year,DaysActive,TotalBreaks,CompletedBreaks,ComplianceRate,TotalActiveHours,AverageBreakMinutes");
            
            foreach (var metric in weeklyMetrics.OrderBy(w => w.WeekStartDate))
            {
                csv.AppendLine($"{metric.WeekStartDate:yyyy-MM-dd},{metric.WeekEndDate:yyyy-MM-dd},{metric.WeekNumber},{metric.Year},{metric.DaysActive},{metric.TotalBreaks},{metric.CompletedBreaks},{metric.ComplianceRate:F3},{metric.TotalActiveTime.TotalHours:F1},{metric.AverageBreakTime.TotalMinutes:F1}");
            }
            
            csv.AppendLine("");
            
            // Monthly metrics section
            csv.AppendLine("## MONTHLY METRICS");
            csv.AppendLine("Month,Year,DaysActive,WeeksActive,TotalBreaks,CompletedBreaks,ComplianceRate,TotalActiveHours,AverageBreakMinutes");
            
            foreach (var metric in monthlyMetrics.OrderBy(m => m.MonthStartDate))
            {
                csv.AppendLine($"{metric.Month:D2},{metric.Year},{metric.DaysActive},{metric.WeeksActive},{metric.TotalBreaks},{metric.CompletedBreaks},{metric.ComplianceRate:F3},{metric.TotalActiveTime.TotalHours:F1},{metric.AverageBreakTime.TotalMinutes:F1}");
            }
            
            return csv.ToString();
        }

        private string ExportToHtml(HealthMetrics health, List<DailyMetric> daily, List<WeeklyMetrics> weekly, List<MonthlyMetrics> monthly)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<title>Eye-rest Analytics Report</title>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Arial,sans-serif;margin:40px;line-height:1.6;color:#333;}");
            html.AppendLine(".header{text-align:center;border-bottom:3px solid #4CAF50;padding-bottom:20px;margin-bottom:30px;}");
            html.AppendLine(".privacy-notice{background:#E8F5E8;border:2px solid #4CAF50;border-radius:8px;padding:15px;margin:20px 0;}");
            html.AppendLine(".summary{display:flex;justify-content:space-around;margin:30px 0;}");
            html.AppendLine(".metric-card{background:#f9f9f9;border-radius:8px;padding:20px;text-align:center;min-width:150px;}");
            html.AppendLine(".metric-value{font-size:2em;font-weight:bold;color:#4CAF50;}");
            html.AppendLine(".metric-label{color:#666;margin-top:5px;}");
            html.AppendLine("table{border-collapse:collapse;width:100%;margin:20px 0;}");
            html.AppendLine("th,td{border:1px solid #ddd;padding:12px;text-align:left;}");
            html.AppendLine("th{background-color:#4CAF50;color:white;font-weight:bold;}");
            html.AppendLine(".section{margin:40px 0;}");
            html.AppendLine(".excellent{color:#4CAF50;font-weight:bold;}");
            html.AppendLine(".good{color:#8BC34A;font-weight:bold;}");
            html.AppendLine(".warning{color:#FFC107;font-weight:bold;}");
            html.AppendLine(".poor{color:#F44336;font-weight:bold;}");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            
            // Header
            html.AppendLine("<div class='header'>");
            html.AppendLine("<h1>📊 Eye-rest Analytics Report</h1>");
            html.AppendLine($"<p>Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine($"<p>Analysis Period: <strong>{health.PeriodStart:yyyy-MM-dd}</strong> to <strong>{health.PeriodEnd:yyyy-MM-dd}</strong></p>");
            html.AppendLine("</div>");
            
            // Privacy notice
            html.AppendLine("<div class='privacy-notice'>");
            html.AppendLine("<h3>🔒 Data Privacy</h3>");
            html.AppendLine("<p>All analytics data is stored locally on your machine only. No information is sent to cloud services, external servers, or third parties. Your privacy is completely protected.</p>");
            html.AppendLine("</div>");
            
            // Summary metrics
            html.AppendLine("<div class='summary'>");
            html.AppendLine("<div class='metric-card'>");
            html.AppendLine($"<div class='metric-value'>{health.ComplianceRate:P0}</div>");
            html.AppendLine("<div class='metric-label'>Overall Compliance</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class='metric-card'>");
            html.AppendLine($"<div class='metric-value'>{health.BreaksCompleted}</div>");
            html.AppendLine("<div class='metric-label'>Breaks Completed</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class='metric-card'>");
            html.AppendLine($"<div class='metric-value'>{health.TotalActiveTime.TotalHours:F1}h</div>");
            html.AppendLine("<div class='metric-label'>Active Time</div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            
            // Monthly metrics
            if (monthly.Count > 0)
            {
                html.AppendLine("<div class='section'>");
                html.AppendLine("<h2>📅 Monthly Performance</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Month</th><th>Days Active</th><th>Total Breaks</th><th>Completed</th><th>Compliance Rate</th><th>Active Time</th></tr>");
                
                foreach (var month in monthly.OrderByDescending(m => m.MonthStartDate))
                {
                    var complianceClass = month.ComplianceRate >= 0.9 ? "excellent" : month.ComplianceRate >= 0.8 ? "good" : month.ComplianceRate >= 0.6 ? "warning" : "poor";
                    html.AppendLine($"<tr><td>{month.MonthText}</td><td>{month.DaysActive}</td><td>{month.TotalBreaks}</td><td>{month.CompletedBreaks}</td><td class='{complianceClass}'>{month.ComplianceRate:P0}</td><td>{month.TotalActiveTime.TotalHours:F1}h</td></tr>");
                }
                
                html.AppendLine("</table>");
                html.AppendLine("</div>");
            }
            
            // Weekly metrics
            if (weekly.Count > 0)
            {
                html.AppendLine("<div class='section'>");
                html.AppendLine("<h2>📊 Weekly Trends</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Week</th><th>Days Active</th><th>Total Breaks</th><th>Completed</th><th>Compliance Rate</th><th>Active Time</th></tr>");
                
                foreach (var week in weekly.OrderByDescending(w => w.WeekStartDate).Take(12))
                {
                    var complianceClass = week.ComplianceRate >= 0.9 ? "excellent" : week.ComplianceRate >= 0.8 ? "good" : week.ComplianceRate >= 0.6 ? "warning" : "poor";
                    html.AppendLine($"<tr><td>{week.WeekText}</td><td>{week.DaysActive}</td><td>{week.TotalBreaks}</td><td>{week.CompletedBreaks}</td><td class='{complianceClass}'>{week.ComplianceRate:P0}</td><td>{week.TotalActiveTime.TotalHours:F1}h</td></tr>");
                }
                
                html.AppendLine("</table>");
                html.AppendLine("</div>");
            }
            
            // Daily breakdown (last 30 days)
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>📈 Daily Details (Recent 30 Days)</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Date</th><th>Breaks Due</th><th>Completed</th><th>Skipped</th><th>Compliance</th><th>Eye Rests</th><th>Break Time</th></tr>");
            
            foreach (var day in daily.OrderByDescending(d => d.Date).Take(30))
            {
                var complianceClass = day.ComplianceRate >= 0.9 ? "excellent" : day.ComplianceRate >= 0.8 ? "good" : day.ComplianceRate >= 0.6 ? "warning" : "poor";
                html.AppendLine($"<tr><td>{day.Date:yyyy-MM-dd}</td><td>{day.BreaksDue}</td><td>{day.BreaksCompleted}</td><td>{day.BreaksSkipped}</td><td class='{complianceClass}'>{day.ComplianceRate:P0}</td><td>{day.EyeRestsCompleted}</td><td>{day.TotalBreakTime.TotalMinutes:F0}min</td></tr>");
            }
            
            html.AppendLine("</table>");
            html.AppendLine("</div>");
            
            // Footer
            html.AppendLine("<div style='text-align:center;margin-top:50px;color:#666;font-size:0.9em;'>");
            html.AppendLine("<p>Generated by Eye-rest Application | Your data remains private and secure</p>");
            html.AppendLine("</div>");
            
            html.AppendLine("</body></html>");
            
            return html.ToString();
        }


        public async Task RecordPauseEventAsync(PauseReason reason)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "INSERT INTO PauseEvents (Timestamp, Reason) VALUES (@timestamp, @reason)";
                    command.Parameters.AddWithValue("@timestamp", DateTime.Now);
                    command.Parameters.AddWithValue("@reason", reason.ToString());
                    command.ExecuteNonQuery();
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording pause event");
            }
        }

        public async Task RecordResumeEventAsync(ResumeReason reason)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "INSERT INTO ResumeEvents (Timestamp, Reason) VALUES (@timestamp, @reason)";
                    command.Parameters.AddWithValue("@timestamp", DateTime.Now);
                    command.Parameters.AddWithValue("@reason", reason.ToString());
                    command.ExecuteNonQuery();
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording resume event");
            }
        }

        public async Task RecordEventAsync(EventHistoryType eventType, string description, Dictionary<string, object?>? metadata = null)
        {
            try
            {
                var metadataJson = metadata != null && metadata.Count > 0
                    ? JsonSerializer.Serialize(metadata)
                    : "{}";

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "INSERT INTO EventHistory (Timestamp, EventType, Description, Metadata, SessionId) VALUES (@timestamp, @eventType, @description, @metadata, @sessionId)";
                    command.Parameters.AddWithValue("@timestamp", DateTime.Now);
                    command.Parameters.AddWithValue("@eventType", eventType.ToString());
                    command.Parameters.AddWithValue("@description", description);
                    command.Parameters.AddWithValue("@metadata", metadataJson);
                    command.Parameters.AddWithValue("@sessionId",
                        _currentSessionId != -1 ? (object)_currentSessionId : DBNull.Value);
                    command.ExecuteNonQuery();
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording event history: {EventType}", eventType);
            }
        }

        public async Task<List<EventHistoryEntry>> GetEventHistoryAsync(DateTime startDate, DateTime endDate, int? limit = null)
        {
            var results = new List<EventHistoryEntry>();
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT Id, Timestamp, EventType, Description, Metadata, SessionId
                        FROM EventHistory
                        WHERE Timestamp >= @startDate AND Timestamp <= @endDate
                        ORDER BY Timestamp DESC"
                        + (limit.HasValue ? " LIMIT @limit" : "");
                    command.Parameters.AddWithValue("@startDate", startDate);
                    command.Parameters.AddWithValue("@endDate", endDate);
                    if (limit.HasValue)
                        command.Parameters.AddWithValue("@limit", limit.Value);

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var entry = new EventHistoryEntry
                        {
                            Id = reader.GetInt32(0),
                            Timestamp = reader.GetDateTime(1),
                            EventType = Enum.TryParse<EventHistoryType>(reader.GetString(2), out var et)
                                ? et : EventHistoryType.EyeRestShown,
                            Description = reader.GetString(3),
                            SessionId = reader.IsDBNull(5) ? null : reader.GetInt32(5)
                        };
                        entry.MetadataJson = reader.IsDBNull(4) ? "{}" : reader.GetString(4);
                        results.Add(entry);
                    }
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event history");
            }
            return results;
        }

        public async Task CleanupOldDataAsync(int retentionDays = 90)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        DELETE FROM RestEvents WHERE TriggeredAt < @cutoffDate;
                        DELETE FROM PresenceEvents WHERE Timestamp < @cutoffDate;
                        DELETE FROM UserSessions WHERE StartTime < @cutoffDate;
                        DELETE FROM PauseEvents WHERE Timestamp < @cutoffDate;
                        DELETE FROM ResumeEvents WHERE Timestamp < @cutoffDate;
                        DELETE FROM EventHistory WHERE Timestamp < @cutoffDate;";

                    command.Parameters.AddWithValue("@cutoffDate", cutoffDate);

                    var deletedRows = command.ExecuteNonQuery();
                    _logger.LogInformation("Cleaned up {DeletedRows} old analytics records older than {CutoffDate}",
                        deletedRows, cutoffDate.ToString("yyyy-MM-dd"));
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old analytics data");
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_currentSessionId != -1)
                {
                    // Use synchronous SQLite operations directly to avoid
                    // synchronization context deadlock from .Wait() on async method
                    var now = DateTime.Now;

                    lock (_sessionLock)
                    {
                        if (_currentSessionState == SessionState.Active)
                        {
                            var activeTime = now - _lastActiveTime;
                            _totalActiveTimeThisSession = _totalActiveTimeThisSession.Add(activeTime);
                        }
                    }

                    lock (_dbLock)
                    {
                        using var connection = new SqliteConnection(_connectionString);
                        connection.Open();

                        using var command = connection.CreateCommand();
                        command.CommandText = @"
                            UPDATE UserSessions
                            SET EndTime = @endTime,
                                TotalActiveTime = @totalActiveTime,
                                InactiveTime = @inactiveTime,
                                SessionState = @sessionState
                            WHERE Id = @sessionId";

                        command.Parameters.AddWithValue("@endTime", now);
                        command.Parameters.AddWithValue("@totalActiveTime", (int)_totalActiveTimeThisSession.TotalMilliseconds);
                        command.Parameters.AddWithValue("@inactiveTime", (int)_totalInactiveTimeThisSession.TotalMilliseconds);
                        command.Parameters.AddWithValue("@sessionState", SessionState.Ended.ToString());
                        command.Parameters.AddWithValue("@sessionId", _currentSessionId);

                        command.ExecuteNonQuery();
                    }

                    _currentSessionId = -1;
                }

                _logger.LogInformation("AnalyticsService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing AnalyticsService");
            }
        }

        private int GetWeekOfYear(DateTime date)
        {
            var dayOfYear = date.DayOfYear;
            return (dayOfYear - 1) / 7 + 1;
        }
    }
}