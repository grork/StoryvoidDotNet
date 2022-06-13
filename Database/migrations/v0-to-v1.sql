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
    liked INTEGER NOT NULL DEFAULT 0,
    UNIQUE(url)
);

CREATE TABLE folders (
    local_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    -- Cant have this as the primary key, since unsynced folders wont have an ID
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
    UNIQUE(article_id)
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
    local_id INTEGER NOT NULL PRIMARY KEY,
    FOREIGN KEY(local_id) REFERENCES folders(local_id)
);

CREATE VIEW folder_adds_with_folder_information AS
    SELECT f.title, a.* FROM folder_adds a
    LEFT JOIN folders f
    ON a.local_id = f.local_id;

CREATE TABLE folder_deletes (
    service_id INTEGER NOT NULL PRIMARY KEY,
    title TEXT NOT NULL,
    UNIQUE(title) -- Titles can't be duplicated on the service; enforce here
);

-- Used when adding a URL, not when adding a bookmark directly; we always need
-- a round trip to the service + sync to get the article visible somewhere
CREATE TABLE article_adds (
    url TEXT NOT NULL PRIMARY KEY,
    title TEXT
);

-- Store article deletes. These only need the actual article ID for a deletion
-- since they can't be resurrected (adding has to go via the service), and don't
-- need any reference to the folder they're in
CREATE TABLE article_deletes (
    id INTEGER NOT NULL PRIMARY KEY
);

-- Like state changes on articles. The presense of an article here implies it is
-- a state *change* from the source of truth. Since state change on the service
-- is idempotent, theres low risk to running it when the state on the service
-- matches. *However*, if we have an unsync'd state change, and the user changes
-- the state again, we're effecting reverting to an unchanges state. e.g. there
-- can only be one article state change at a time (hence the PK on the article
-- id).
-- 
-- Also note that isn't a FK reference to the articles table. This is because
-- you may like/unlike an article and then delete it; we need to sync that. For
-- unliking, this isn't significant. However, since the instapaper service pushes
-- out tweets etc for liked articles, you might like to share, and then purge.
CREATE TABLE article_liked_changes (
    article_id INTEGER NOT NULL PRIMARY KEY,
    liked BOOLEAN
);

-- Track article moves between folders. There is only expected to be one folder
-- change per article, even if the article is moved multiple times; only one
-- change is needed on the service.
CREATE TABLE article_folder_changes (
    article_id INTEGER NOT NULL PRIMARY KEY,
    destination_local_id INTEGER NOT NULL,

    FOREIGN KEY(article_id) REFERENCES articles(id),
    FOREIGN KEY(destination_local_id) REFERENCES folders(local_id)
);

-- Set version to indicate default state created
PRAGMA user_version = 1;