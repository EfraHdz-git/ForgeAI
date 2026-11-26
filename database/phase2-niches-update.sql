USE ForgeAI;

-- ============================================
-- TIGHTEN existing broad niches
-- ============================================

-- Content Creators was too broad (covered YouTube+TikTok+newsletters+podcasts). Narrow to newsletters/blogs since we're splitting out video platforms.
UPDATE focus_profiles SET
  display_name = 'Newsletter & Blog Creators',
  included_users = 'newsletter writers, bloggers, Substack creators, Medium writers, niche blog operators, email list builders',
  excluded_users = 'video creators, social media influencers, enterprise content teams, journalism outlets',
  included_problems = 'growing subscriber list, monetizing newsletter, topic ideation, open rate optimization, cross-promotion, managing paid tiers, writing consistency, audience segmentation',
  excluded_problems = 'video editing, thumbnail design, social media scheduling, streaming setup',
  example_use_cases = 'A Substack writer has 500 subscribers but no idea which topics drive opens vs unsubscribes|A blogger publishes weekly but gets zero organic traffic and doesnt know why|A newsletter creator wants to add a paid tier but doesnt know what to gate',
  source_config = '{"hn":true,"producthunt":{"tags":["writing","newsletters","content"]},"google_trends":{"queries":["newsletter tool","substack alternative","blog monetization"]}}'
WHERE name = 'content_creators';

-- Education was too broad. Focus on self-directed adult learning.
UPDATE focus_profiles SET
  display_name = 'Self-Learning & Skill Building',
  included_users = 'self-learners, career changers, bootcamp students, skill stackers, professionals upskilling, hobbyist learners',
  excluded_users = 'K-12 students, universities, corporate training departments, LMS administrators',
  included_problems = 'not knowing what to learn next, lack of structured path, no accountability, cant measure progress, tutorial hell, forgotten knowledge, skill gap identification',
  excluded_problems = 'classroom management, grading systems, academic administration, standardized testing',
  example_use_cases = 'A career changer wants to learn data science but doesnt know what to study first or in what order|A developer learns React tutorials but cant build anything from scratch|A professional wants to upskill but has only 30 minutes per day',
  source_config = '{"hn":true,"producthunt":{"tags":["education","learning","productivity"]},"google_trends":{"queries":["learn to code app","skill tracker","online learning path"]}}'
WHERE name = 'education';

-- Fitness was broad. Focus on gym/workout adherence specifically.
UPDATE focus_profiles SET
  display_name = 'Gym & Workout Tracking',
  included_users = 'gym beginners, home workout enthusiasts, runners, weightlifters tracking progress, people returning to fitness after a break',
  excluded_users = 'professional athletes, certified personal trainers (as business), clinical rehabilitation, nutritionists',
  included_problems = 'forgetting workout routine mid-session, not tracking progressive overload, skipping rest days, no accountability partner, inconsistent form, not knowing what to do at the gym',
  excluded_problems = 'meal planning, calorie counting, supplement tracking, medical rehabilitation, sports team coordination',
  example_use_cases = 'A gym beginner stands in front of a squat rack with no idea what weight to use or how many sets|A runner wants to improve their 5K time but has no structured plan|A person returning to gym after 2 years doesnt know where to start',
  source_config = '{"hn":false,"producthunt":{"tags":["fitness","health"]},"google_trends":{"queries":["workout tracker app","gym routine app","weightlifting log"]}}'
WHERE name = 'fitness';

-- Gaming was broad. Focus on game discovery and backlog management.
UPDATE focus_profiles SET
  display_name = 'Game Discovery & Backlog',
  included_users = 'casual gamers with large libraries, Steam/PlayStation/Xbox users, indie game enthusiasts, gaming parents managing screen time',
  excluded_users = 'game developers, esports professionals, game streamers as business, game reviewers',
  included_problems = 'too many games not knowing what to play, unfinished games piling up, discovering indie gems, tracking playtime across platforms, coordinating multiplayer sessions with friends',
  excluded_problems = 'game development, streaming setup, competitive ranking, game modding',
  example_use_cases = 'A gamer has 300 games across Steam Epic and PlayStation and spends 20 minutes deciding what to play then gives up|A parent wants to find age-appropriate games without reading 50 reviews|A friend group tries to find a game they all own to play together',
  source_config = '{"hn":false,"producthunt":{"tags":["gaming","entertainment"]},"google_trends":{"queries":["game backlog tracker","what game should I play","game recommendation app"]}}'
WHERE name = 'gaming';

-- Finance was broad. Focus on personal budgeting specifically.
UPDATE focus_profiles SET
  display_name = 'Personal Budgeting & Debt',
  included_users = 'people in debt, young adults managing money for the first time, gig workers with irregular income, couples merging finances, people trying to save for a goal',
  excluded_users = 'investors, day traders, financial advisors, accountants, crypto traders',
  included_problems = 'not knowing where money goes, cant stick to a budget, irregular income makes planning impossible, credit card debt spiral, splitting expenses with partner, saving for specific goals',
  excluded_problems = 'investment portfolio management, stock trading, tax optimization for businesses, crypto tracking',
  example_use_cases = 'A 25-year-old has no idea how much they spend on subscriptions each month|A gig worker earns different amounts each week and cant plan ahead|A couple argues about money because neither tracks shared expenses',
  source_config = '{"hn":true,"producthunt":{"tags":["fintech","personal-finance"]},"google_trends":{"queries":["budget app","debt payoff tracker","expense tracker app"]}}'
WHERE name = 'finance';

-- ============================================
-- ADD new specific niches
-- ============================================

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('youtube', 'YouTube Creators', 1, 'small YouTubers under 10K subs, YouTube beginners, faceless channel operators, educational YouTubers, niche content creators', 'large established channels, MCNs, YouTube employees, professional video production studios', 'getting first 1000 subscribers, title and thumbnail optimization, understanding algorithm, content ideation, retention analysis, scheduling consistency, burnout, niche selection, monetization before AdSense', 'professional video editing, studio equipment, live streaming infrastructure, enterprise video hosting', 'hybrid', 'freemium, subscription', 'A small YouTuber gets 200 views per video and has no idea if its their titles thumbnails or content thats the problem|A faceless channel operator wants to find profitable niches without showing their face|A creator posts weekly but their subscriber count hasnt moved in 3 months', '{"hn":false,"producthunt":{"tags":["youtube","creator-tools","video"]},"google_trends":{"queries":["youtube growth tool","thumbnail maker","youtube analytics app"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('tiktok', 'TikTok & Short-Form Video', 1, 'TikTok creators, Instagram Reels creators, YouTube Shorts creators, small brand accounts, side hustle creators using short video', 'large influencers, talent agencies, TikTok employees, enterprise social media teams', 'going viral vs consistent growth, trending sound discovery, optimal posting times, content repurposing across platforms, monetizing small audiences, creator fund alternatives, niche finding, hook writing', 'professional video production, brand management at scale, influencer agency tools', 'mobile', 'freemium, subscription', 'A TikTok creator gets random viral videos but cant sustain consistent views|A small business owner wants to use TikTok for marketing but doesnt know what content works|A creator posts on TikTok Reels and Shorts manually and wastes hours repurposing', '{"hn":false,"producthunt":{"tags":["tiktok","social-media","creator-tools"]},"google_trends":{"queries":["tiktok analytics","trending sounds app","short form video tool"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('freelance', 'Freelancers & Solopreneurs', 1, 'freelance designers, freelance developers, copywriters, virtual assistants, consultants, solo service providers', 'agencies, staffing firms, enterprise contractors, full-time employees', 'finding clients, pricing services, managing invoices, tracking time, scope creep, contract management, feast-or-famine income cycles, client communication overload', 'enterprise project management, agency workflow, corporate procurement', 'web', 'subscription, freemium', 'A freelance designer spends 30% of their time on admin instead of design|A consultant has no idea if their hourly rate is competitive or leaving money on the table|A freelancer finished a project 2 weeks ago and still hasnt been paid', '{"hn":true,"producthunt":{"tags":["freelance","productivity","invoicing"]},"google_trends":{"queries":["freelance invoice app","client management tool","freelancer pricing"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('remote_work', 'Remote Work & Async Teams', 1, 'remote workers, distributed teams, async-first companies, digital nomad teams, hybrid workers', 'office-only companies, coworking space operators, enterprise IT', 'timezone coordination, async communication overload, meeting fatigue, isolation, work-life boundary, status updates across tools, documentation gaps, onboarding remote hires', 'office space management, in-person event planning, corporate real estate', 'web', 'subscription, per-seat', 'A remote team across 4 timezones spends half their day in status update meetings|A new remote hire has no idea what to work on for their first week because documentation is scattered|A remote worker cant disconnect because Slack pings come at all hours', '{"hn":true,"producthunt":{"tags":["remote-work","productivity","team-tools"]},"google_trends":{"queries":["async work tool","remote team management","timezone meeting scheduler"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('mental_health', 'Mental Health & Wellness', 1, 'people managing anxiety, stress management seekers, journalers, meditation beginners, therapy-adjacent self-help users', 'licensed therapists as practitioners, psychiatric professionals, clinical researchers, insurance companies', 'tracking mood patterns, building consistent habits, journaling without knowing what to write, meditation consistency, sleep hygiene, managing anxiety triggers, finding affordable support', 'clinical diagnosis tools, EMR systems, psychiatric medication management, hospital systems', 'mobile', 'subscription, freemium', 'A person with anxiety wants to track what triggers bad days but doesnt know how to start|A journaler stares at a blank page every night and gives up|Someone wants to meditate daily but quits after 3 days every time', '{"hn":false,"producthunt":{"tags":["mental-health","wellness","mindfulness"]},"google_trends":{"queries":["mood tracker app","anxiety journal","meditation app for beginners"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('ecommerce', 'E-commerce & Online Selling', 1, 'Shopify store owners, Etsy sellers, dropshippers, Amazon FBA sellers, print-on-demand operators, small DTC brands', 'enterprise retail, Amazon corporate, large marketplace operators, wholesale distributors', 'product research, finding winning products, inventory management, shipping cost optimization, review management, abandoned cart recovery, pricing strategy, product photography', 'enterprise ERP, warehouse management at scale, supply chain optimization', 'web', 'subscription, freemium', 'An Etsy seller has 50 products but only 3 sell and doesnt know why|A dropshipper tests 10 products per week and wastes money on ads for losers|A Shopify store owner has 2% conversion rate and no idea whats broken', '{"hn":true,"producthunt":{"tags":["e-commerce","shopify","online-store"]},"google_trends":{"queries":["product research tool","shopify app","dropshipping tool"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('podcasting', 'Podcast Creators', 1, 'new podcasters, indie podcast hosts, interview-format shows, solo commentary creators, niche topic podcasters', 'podcast networks, enterprise media companies, radio stations, professional studios', 'growing listeners, guest booking, episode planning, show notes generation, distribution across platforms, monetization before sponsorships, audio quality on a budget, consistency', 'professional audio engineering, broadcast infrastructure, large-scale ad insertion', 'hybrid', 'subscription, freemium', 'A podcaster has 50 episodes but only 30 downloads per episode and doesnt know how to grow|A solo podcaster spends 2 hours writing show notes for every 30 minute episode|A new podcaster wants to book guests but has no network and no pitch template', '{"hn":false,"producthunt":{"tags":["podcasting","audio","creator-tools"]},"google_trends":{"queries":["podcast growth tool","show notes generator","podcast hosting"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('job_search', 'Job Search & Career', 1, 'job seekers, career changers, recent graduates, people returning to work after a gap, interview preppers', 'recruiters, HR departments, staffing agencies, career coaches as business', 'writing resumes that pass ATS, cover letter fatigue, interview preparation, salary negotiation, tracking applications across platforms, networking without feeling sleazy, personal branding on LinkedIn', 'enterprise ATS systems, recruitment CRM, corporate HR platforms', 'web', 'freemium, per-use', 'A job seeker applies to 50 jobs and gets zero callbacks because their resume doesnt pass ATS|A career changer doesnt know how to position 10 years of unrelated experience|A recent grad has no idea what salary to ask for and always lowballs themselves', '{"hn":true,"producthunt":{"tags":["career","job-search","productivity"]},"google_trends":{"queries":["resume builder","interview prep app","salary negotiation tool"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('photography', 'Photography & Visual Arts', 1, 'amateur photographers, wedding photographers starting out, Instagram photographers, product photographers, photo editors', 'professional photo agencies, stock photo companies, camera manufacturers', 'organizing thousands of photos, consistent editing style, client delivery workflow, portfolio building, finding photography clients, pricing shoots, learning composition', 'professional studio management, enterprise DAM systems, stock photo infrastructure', 'hybrid', 'subscription, freemium', 'A photographer has 50000 photos across 3 hard drives and cant find anything|A wedding photographer sends files via Google Drive and it feels unprofessional|An Instagram photographer edits every photo differently and has no consistent brand look', '{"hn":false,"producthunt":{"tags":["photography","design","creative-tools"]},"google_trends":{"queries":["photo organizer app","photography portfolio","client gallery tool"]}}');

INSERT INTO focus_profiles (name, display_name, is_active, included_users, excluded_users, included_problems, excluded_problems, platform_bias, monetization_pref, example_use_cases, source_config) VALUES
('sustainability', 'Sustainability & Green Living', 1, 'eco-conscious consumers, zero-waste beginners, people tracking carbon footprint, sustainable shoppers, community garden members', 'corporate sustainability officers, environmental NGOs, industrial waste management', 'finding genuinely sustainable products vs greenwashing, tracking personal carbon footprint, reducing food waste at home, finding local sustainable options, building eco habits gradually', 'enterprise ESG reporting, industrial emissions tracking, corporate supply chain sustainability', 'mobile', 'freemium, subscription', 'A person wants to reduce their carbon footprint but has no idea where to start or what matters most|A shopper cant tell if a product is actually sustainable or just marketing|A family throws away $50 of food per week and wants to stop but forgets whats in the fridge', '{"hn":false,"producthunt":{"tags":["sustainability","green","lifestyle"]},"google_trends":{"queries":["carbon footprint tracker","sustainable shopping app","zero waste app"]}}');

SELECT COUNT(*) AS total, 'niches total' AS label FROM focus_profiles;
