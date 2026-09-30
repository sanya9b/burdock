import json, glob
CANDS = """
encrypt encryption decrypt authenticate authentication authorization vulnerability malware phishing firewall
bandwidth latency outage downtime glitch compatibility configure configuration deploy deployment debug algorithm
interface cache compress compression encode decode retrieve retrieval execute compile syntax parameter framework
module dependency repository patch migrate migration virtualization hosting protocol router proxy gateway node
cluster open-source proprietary plugin extension shortcut toggle touchscreen peripheral firmware processor
semiconductor binary pixel driver kernel operating_system reboot boot wireless hotspot roaming spam thread
unsubscribe notification pop-up tracker surveillance anonymity anonymous avatar username passphrase
two-factor_authentication biometric fingerprint facial_recognition credential login logout permission administrator
privilege exploit ransomware spyware trojan bot scam scammer identity_theft data_breach breach troll moderator
moderation ban censorship parental_controls screen_time digital_detox digital_footprint tech-savvy digital_literacy
user-friendly intuitive clunky bloated lightweight refactor technical_debt legacy obsolete obsolescence deprecated
backward_compatible cross-platform sync synchronize streaming buffer buffering playback thumbnail screenshot headset
earbuds wearable smartwatch smart_home voice_assistant automate automation machine_learning artificial_intelligence
neural_network chatbot prompt generative dataset big_data analytics dashboard formula macro workflow integration
backend frontend developer coder agile sprint backlog ticket release beta prototype mockup wireframe usability
user_experience user_interface accessibility responsive layout template theme dark_mode font emoji meme clipboard
drag_and_drop undo redo autocomplete autocorrect predictive_text trackpad ergonomic adapter dongle port charger
power_bank surge_protector overheat throttle benchmark specification specs high-end low-end entry-level flagship
subscription in-app_purchase microtransaction ad_blocker monetize monetization clickbait engagement retention
onboarding tutorial walkthrough troubleshoot troubleshooting diagnostic error_message crash_report unresponsive
lag laggy frame_rate bottleneck overload redundancy restore recovery snapshot replicate archive purge wipe
partition corrupt corrupted integrity checksum hash token cipher blockchain cryptocurrency wallet decentralized
centralized interoperability interoperable standardize sandbox rollout roll_back hotfix zero-day whitelist
blacklist quarantine backdoor brute_force social_engineering spoofing spoof impersonate doxxing cyberbullying
shadowban deplatform takedown timeline viral influencer subscriber streamer hashtag trending doomscrolling unplug
paywall freemium crowdsource crowdsourcing gig_economy remote_work telecommute video_call mute unmute
screen_share fiber-optic cellular dead_zone reception tethering
sim_card unlock jailbreak sideload app_store uninstall reinstall factory_reset reset bricked safe_mode
cybersecurity cyberattack cybercrime hacktivist keylogger honeypot penetration_testing
end-to-end_encryption two-step_verification captcha deepfake misinformation disinformation
fact-check echo_chamber filter_bubble algorithmic personalization targeted_advertising opt_in opt_out
consent terms_of_service end_user data_mining data_center supercomputer quantum_computing
cloud_storage cloud_computing on-premises edge_computing internet_of_things smart_device smart_speaker
connectivity real-time offline_mode airplane_mode
battery_life fast_charging wireless_charging uptime scalable scalability
throughput data_plan carrier telecom telecommunications
coverage mesh_network access_point ethernet docking_station
refresh_rate aspect_ratio high-definition
graphics_card hard_drive solid-state_drive flash_drive external_drive memory_card
motherboard overclock power_supply chassis form_factor
desktop workstation terminal command_line shell
scripting cron_job background_process task_manager
process multitasking multithreading parallel_processing concurrency asynchronous synchronous
callback event-driven listener handler trigger loop iteration iterate recursion recursive conditional
variable constant function method class object instance inherit inheritance encapsulation abstraction
array string integer boolean null pointer reference
runtime exception error_handling stack_trace breakpoint unit_test test_coverage
regression bug_report feature_request pull_request merge merge_conflict branch commit rollback version_control
versioning changelog release_notes long-term_support end_of_life sunset deprecate
open_standard file_format compressed_file zip unzip
metadata tag tagging indexing index query relational_database
schema primary_key data_model data_warehouse
pipeline ingest ingestion batch_processing
visualization heat_map infographic open_data
telemetry log_file logging audit_trail monitoring alerting incident postmortem
root_cause status_page availability reliability resilience fault_tolerance failover
load_balancing rate_limit throttling quota tier free_tier enterprise enterprise_software saas
license licensing shareware freeware bloatware adware
antivirus spam_filter junk_folder email_thread
reply-all megabyte gigabyte terabyte kilobyte byte bit
bitrate lossy lossless codec transcoding rendering render
computer-aided animation motion_capture green_screen
augmented_reality mixed_reality metaverse immersive haptic haptics
simulation emulator emulation modding mod esports
livestream livestreaming vlogger content_creator creator_economy
affiliate_link referral_code promo_code
uninstall reinstall
""".split()
PHRASES = ["log in","log out","sign in","sign out","back up","set up","plug in","power up","power down","shut down",
"scroll down","double-click","right-click","long press",
"pinch to zoom","copy and paste","search bar","address bar","status bar",
"home screen","lock screen","screen saver","wallpaper","ringtone","silent mode","do not disturb",
"low power mode","battery saver","night mode","incognito mode","private browsing",
"clear cache","browsing history","search history","autofill","password manager",
"strong password","password reset","security question","verification code",
"one-time password","authenticator app","suspicious activity",
"account takeover","account recovery","locked out","session expired","time out","timed out",
"slow connection","connection dropped","dropped call","poor signal",
"spinning wheel","frozen screen","black screen","blue screen","error code",
"force quit","hard reset","factory settings","default settings","advanced settings",
"privacy settings","developer options","easter egg",
"beta version","stable release","nightly build","release candidate","early access","closed beta","open beta",
"legacy system","legacy code","spaghetti code","clean code","code base","source code","open source","closed source",
"machine code","high-level language","low-level language","assembly language",
"programming language","scripting language","markup language","query language","natural language processing",
"speech recognition","voice recognition","image recognition","pattern recognition","computer vision","self-driving",
"autonomous vehicle","smart contract","digital wallet","mobile payment","contactless payment",
"online banking","digital signature","electronic signature",
"digital certificate","public key","private key","key exchange","secure connection",
"digital rights management","copy protection","region lock","geo-blocking",
"safe search","age verification","cookie banner","unsubscribe link",
"mailing list","newsletter","spam folder","phishing email","scam call","robocall","spoofed number","caller ID",
"call screening","call blocking","block list","allow list","contact list","address book",
"cloud backup","restore point","system restore","recovery drive",
"boot disk","dual boot","file system","disk space","free space","storage space",
"disk cleanup",
"app permissions","location services","location tracking","GPS tracking","remote wipe",
"screen lock","face unlock","fingerprint unlock","pattern lock","PIN code","passcode",
"carrier lock","unlocked phone","refurbished","certified refurbished",
"trade-in","upgrade cycle","planned obsolescence","right to repair","repairability","modular design","user-replaceable",
"non-removable","soldered","proprietary connector","charging port",
"headphone jack","expansion slot","card reader","optical drive","disc drive","floppy disk",
"hard copy","soft copy","printout","print preview","print queue","paper jam","toner","ink cartridge",
"document scanner","barcode scanner","QR code","barcode","RFID tag",
"NFC tag","smart card","key fob","access card","key card","magnetic stripe",
"tech support","help desk","service desk","remote access","remote desktop","screen sharing","screen mirroring","cast",
"brick","bricking","roll out","phase out","scale up","scale down","spin up","tear down","kick off","kick in",
"go live","go down","be down","take down","bring down","back end","front end","full stack","full-stack",
"data entry","data breach","data leak","data loss","data recovery","data protection","data privacy","data retention",
"user data","personal data","sensitive data","raw data","big data","metadata","open source software",
"machine-readable","human-readable","plain text","rich text","source file","binary file","executable","installer",
"wizard","setup wizard","progress bar","loading screen","splash screen","landing page","home page","web page",
"drop-down","drop-down menu","checkbox","radio button","slider","toggle switch","text field","input field","tooltip",
"hover","click-through","pop-under","overlay","modal","sidebar","navigation bar","breadcrumb","footer","header",
"hamburger menu","infinite scroll","pull to refresh","swipe to delete","dark pattern","nudge","friction","frictionless",
"seamless","glitchy","buggy","laggy","janky","snappy","zippy","sluggish","bloat","cruft","kludge","hack","workaround",
"hotfix","quick fix","band-aid fix","duct tape","patch up","bodge","fudge","hardcode","hardcoded","boilerplate",
"scaffold","scaffolding","stub","mock","dummy data","placeholder","lorem ipsum","sample data","test data","seed data",
"edge case","corner case","happy path","worst case","race condition","deadlock","memory leak","buffer overflow",
"stack overflow","null pointer","off-by-one","infinite loop","crash loop","boot loop","kernel panic","segfault",
"core dump","heap","stack","garbage collection","garbage collector","compiler","interpreter","transpiler","linker",
"assembler","debugger","profiler","linter","formatter","IDE","text editor","code editor","terminal emulator",
"command prompt","command-line interface","graphical user interface","touch interface","voice interface",
"natural language","chat interface","conversational","assistant","copilot","agent","autonomous agent","agentic"]
base = "D:/WORK/SOURCE_CODE/Burdock/content/generated/uk-en/inventory/"
lem = set(l.strip().lower() for l in open(base + "lemmas.txt", encoding="utf-8") if l.strip())
other = {}
for f in glob.glob(base + "proposals/*.json"):
    d = json.load(open(f, encoding="utf-8"))
    for x in d["lexemes"]:
        other.setdefault(x["lemma"].lower(), []).append(d["area"])
seen, ok, dup, oth = set(), [], [], []
for c in [c.replace("_", " ") for c in CANDS] + PHRASES:
    k = c.lower()
    if k in seen:
        continue
    seen.add(k)
    if k in lem:
        dup.append(c)
    elif k in other:
        oth.append("%s(%s)" % (c, ",".join(other[k])))
    else:
        ok.append(c)
print("IN LEMMAS (%d):" % len(dup)); print(", ".join(dup)); print()
print("IN OTHER PROPOSALS (%d):" % len(oth)); print(", ".join(oth)); print()
print("AVAILABLE (%d):" % len(ok)); print(", ".join(ok))
