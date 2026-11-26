USE ForgeAI;

-- Users
CREATE TABLE users (
    id INT IDENTITY(1,1) PRIMARY KEY,
    email NVARCHAR(255) NOT NULL UNIQUE,
    name NVARCHAR(255),
    avatar_url NVARCHAR(500),
    tier NVARCHAR(20) NOT NULL DEFAULT 'free',
    reports_used INT NOT NULL DEFAULT 0,
    prototypes_used INT NOT NULL DEFAULT 0,
    reports_reset_at DATETIME2 DEFAULT GETUTCDATE(),
    stripe_customer_id NVARCHAR(255),
    stripe_subscription_id NVARCHAR(255),
    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    last_login_at DATETIME2
);

-- Magic links for passwordless auth
CREATE TABLE magic_links (
    id INT IDENTITY(1,1) PRIMARY KEY,
    email NVARCHAR(255) NOT NULL,
    token NVARCHAR(128) NOT NULL UNIQUE,
    expires_at DATETIME2 NOT NULL,
    used_at DATETIME2,
    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- User sessions (JWT refresh tracking)
CREATE TABLE user_sessions (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id),
    token NVARCHAR(512) NOT NULL UNIQUE,
    expires_at DATETIME2 NOT NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Link opportunities to users (nullable = internal pipeline)
ALTER TABLE opportunities ADD user_id INT NULL REFERENCES users(id);

-- Indexes
CREATE INDEX IX_magic_links_token ON magic_links(token);
CREATE INDEX IX_magic_links_email ON magic_links(email);
CREATE INDEX IX_user_sessions_token ON user_sessions(token);
CREATE INDEX IX_user_sessions_user_id ON user_sessions(user_id);
CREATE INDEX IX_opportunities_user_id ON opportunities(user_id);

SELECT 'Auth tables created successfully' AS result;
