CREATE TABLE bookmarks (
    id INTEGER NOT NULL PRIMARY KEY,
    url TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    progress REAL NOT NULL DEFAULT 0.0,
    progress_timestamp INTEGER NOT NULL DEFAULT 0,
    hash TEXT NOT NULL,
    liked INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE folders (
    local_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    service_id INTEGER, -- Cant have this as a primary key, since temporary folders wont have an ID
    title TEXT NOT NULL,
    position INTEGER NOT NULL DEFAULT 0,
    sync_to_mobile INTEGER NOT NULL DEFAULT 1,
    UNIQUE(service_id),
    UNIQUE(title)
);

-- Insert the system folders
INSERT INTO folders(service_id, title)
VALUES (-1, 'Home');

INSERT INTO folders(service_id, title)
VALUES (-2, 'Archive');

CREATE TABLE folders_to_bookmarks (
    pair_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    local_folder_id INTEGER NOT NULL DEFAULT 0,
    bookmark_id INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(local_folder_id) REFERENCES folders(local_id), 
    FOREIGN KEY(bookmark_id) REFERENCES bookmarks(id)
);

CREATE TABLE bookmark_download_state (
    bookmark_id INTEGER NOT NULL PRIMARY KEY,
    available_locally INTEGER NOT NULL DEFAULT 0,
    has_images INTEGER NOT NULL DEFAULT 0,
    first_image_local_path TEXT,
    first_image_remote_path TEXT,
    local_path TEXT,
    extracted_description TEXT,
    article_unavailable INTEGER NOT NULL DEFAULT 0,
    include_in_mru INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY(bookmark_id) REFERENCES bookmarks(id)
);

CREATE TABLE bookmark_changes (
    change_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    operation TEXT NOT NULL,
    bookmark_id INTEGER NOT NULL DEFAULT 0,
    payload TEXT
);

CREATE TABLE folder_changes (
    change_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    operation TEXT NOT NULL,
    local_id INTEGER NOT NULL DEFAULT 0,
    service_id INTEGER NOT NULL DEFAULT 0,
    payload TEXT
);

-- Set version to indicate default state created
PRAGMA user_version = 1;