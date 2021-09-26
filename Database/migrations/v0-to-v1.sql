-- Enable Write Ahead Logging (https://sqlite.org/wal.html)
PRAGMA journal_mode = 'wal';

CREATE TABLE articles (
    id INTEGER NOT NULL PRIMARY KEY,
    url TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    read_progress REAL NOT NULL DEFAULT 0.0,
    read_progress_timestamp INTEGER NOT NULL DEFAULT 0,
    hash TEXT NOT NULL,
    liked INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE folders (
    local_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    -- Cant have this as a primary key, since temporary folders wont have an ID
    service_id INTEGER,
    title TEXT NOT NULL,
    position INTEGER NOT NULL DEFAULT 0,
    should_sync INTEGER NOT NULL DEFAULT 1,
    UNIQUE(service_id),
    UNIQUE(title)
);

-- Insert the system folders
INSERT INTO folders(local_id, service_id, title, position)
VALUES (1, -1, 'Home', -100);

INSERT INTO folders(local_id, service_id, title, position)
VALUES (2, -2, 'Archive', -99);

CREATE TABLE article_to_folder (
    pair_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    local_folder_id INTEGER NOT NULL DEFAULT 0,
    article_id INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(local_folder_id) REFERENCES folders(local_id), 
    FOREIGN KEY(article_id) REFERENCES articles(id)
);

CREATE TABLE article_local_only_state (
    article_id INTEGER NOT NULL PRIMARY KEY,
    available_locally INTEGER NOT NULL DEFAULT 0,
    first_image_local_path TEXT,
    first_image_remote_path TEXT,
    local_path TEXT,
    extracted_description TEXT,
    article_unavailable INTEGER NOT NULL DEFAULT 0,
    include_in_mru INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY(article_id) REFERENCES articles(id)
);

-- View to bundle up the articles w/ their download state to be abstract away
-- some of the source information from the wrapper library
CREATE VIEW articles_with_local_only_state AS
    SELECT * FROM articles
    LEFT JOIN article_local_only_state
    ON articles.id = article_local_only_state.article_id;

-- Change Tracking tables
CREATE TABLE folder_adds (
    change_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    local_id INTEGER NOT NULL,
    UNIQUE(local_id),
    FOREIGN KEY(local_id) REFERENCES folders(local_id)
);

CREATE VIEW folder_adds_with_folder_information AS
    SELECT f.title, a.* FROM folder_adds a
    LEFT JOIN folders f
    ON a.local_id = f.local_id;

CREATE TABLE folder_deletes (
    change_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    service_id INTEGER NOT NULL,
    title TEXT NOT NULL,
    UNIQUE(service_id),
    UNIQUE(title) -- Titles can't be duplicated on the service; enforce here
);

-- Set version to indicate default state created
PRAGMA user_version = 1;