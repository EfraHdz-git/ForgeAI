USE ForgeAI;
GO

-- Phase 1: Users + auth
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
BEGIN
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
        last_login_at DATETIME2,
        password_hash NVARCHAR(255),
        email_verified BIT NOT NULL DEFAULT 0,
        email_verify_token NVARCHAR(128),
        is_admin BIT NOT NULL DEFAULT 0
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'magic_links')
BEGIN
    CREATE TABLE magic_links (
        id INT IDENTITY(1,1) PRIMARY KEY,
        email NVARCHAR(255) NOT NULL,
        token NVARCHAR(128) NOT NULL UNIQUE,
        expires_at DATETIME2 NOT NULL,
        used_at DATETIME2,
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_magic_links_token ON magic_links(token);
    CREATE INDEX IX_magic_links_email ON magic_links(email);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'user_sessions')
BEGIN
    CREATE TABLE user_sessions (
        id INT IDENTITY(1,1) PRIMARY KEY,
        user_id INT NOT NULL REFERENCES users(id),
        token NVARCHAR(512) NOT NULL UNIQUE,
        expires_at DATETIME2 NOT NULL,
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_user_sessions_token ON user_sessions(token);
    CREATE INDEX IX_user_sessions_user_id ON user_sessions(user_id);
END;
GO

-- Add user_id to opportunities
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('opportunities') AND name = 'user_id')
BEGIN
    ALTER TABLE opportunities ADD user_id INT NULL REFERENCES users(id);
    CREATE INDEX IX_opportunities_user_id ON opportunities(user_id);
END;
GO

-- Phase 3: Share tokens
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'share_tokens')
BEGIN
    CREATE TABLE share_tokens (
        id INT IDENTITY(1,1) PRIMARY KEY,
        opportunity_id INT NOT NULL REFERENCES opportunities(id),
        user_id INT NOT NULL REFERENCES users(id),
        token NVARCHAR(128) NOT NULL UNIQUE,
        type NVARCHAR(20) NOT NULL,
        expires_at DATETIME2,
        is_active BIT NOT NULL DEFAULT 1,
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        view_count INT NOT NULL DEFAULT 0,
        last_viewed_at DATETIME2
    );
    CREATE INDEX IX_share_tokens_token ON share_tokens(token);
    CREATE INDEX IX_share_tokens_opportunity ON share_tokens(opportunity_id);
END;
GO

SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE name IN ('users','magic_links','user_sessions','share_tokens')) AS auth_tables_present,
    (SELECT COUNT(*) FROM focus_profiles) AS niche_count;
GO
