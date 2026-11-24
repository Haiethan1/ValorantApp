CREATE TABLE QueryLog (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    query_text TEXT NOT NULL,
    parameters TEXT,               -- Stores parameter names and values as a string (e.g., "param1=value1, param2=value2")
    execution_time INTEGER,        -- Stores execution time in milliseconds
    retry_count INTEGER,           -- Number of retry, max 3
    success BOOLEAN,
    timestamp INTEGER DEFAULT (strftime('%s', 'now'))
);