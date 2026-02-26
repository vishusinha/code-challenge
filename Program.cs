// Program.cs
// Target: .NET Framework 4.7.2+ (4.8 recommended for Hyland.Unity)
// NuGet: Newtonsoft.Json
// References: Hyland.Unity.dll (+ Hyland.Types if required)

using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using Hyland.Unity;
using Newtonsoft.Json;

namespace LitigationHold
{
    // ---------------- Models matching JSON from Power Automate ----------------
    public class LitigationHoldRequest
    {
        public LitigationHold LitigationHold { get; set; }
    }

    public class LitigationHold
    {
        public string Date { get; set; }
        public string Re { get; set; }
        public Person From { get; set; }
        public List<Person> To { get; set; }
        public List<Person> CC { get; set; }
        public List<Person> Employees { get; set; }

        public string SenderEmailID { get; set; }
        public Person CaseSentBy { get; set; }
    }

    public class Person
    {
        public string Name { get; set; }
        public string Designation { get; set; }
    }

    // Used for keyword records in OnBase
    public class EmployeeKeywordRecordData
    {
        public string Name { get; set; }
        public string Designation { get; set; }
        public string FolderName { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }

        // Role identifier stored in employee keyword record keyword type "MAIL"
        public string MailRole { get; set; } // FROM/TO/CC/(blank)/SENDER
    }

    // ---------------- Config ----------------
    public class AppConfig
    {
        // Folder paths (OneDrive synced)
        public string IncomingFolder { get; set; } =
            @"C:\Users\vsinha\OneDrive - Arlington County Government\Documents\LitHold\incoming";
        public string ProcessedFolder { get; set; } =
            @"C:\Users\vsinha\OneDrive - Arlington County Government\Documents\LitHold\processed";
        public string ErrorFolder { get; set; } =
            @"C:\Users\vsinha\OneDrive - Arlington County Government\Documents\LitHold\error";

        // Local file tracking next case number
        public string CaseCounterFile { get; set; } =
            @"C:\Users\vsinha\OneDrive - Arlington County Government\Documents\LitHold\case_counter.txt";

        // OnBase connection — read from environment variables to avoid hardcoded credentials.
        // Set these on the machine running this console app:
        //   LITHOLD_APPSERVER_URL, LITHOLD_USERNAME, LITHOLD_PASSWORD, LITHOLD_DATASOURCE
        public string AppServerUrl { get; set; } =
            Env("LITHOLD_APPSERVER_URL", @"https://qa-ermscor-az.arlingtonva.us/AppServerAdmin64-24-1-11/Service.asmx");
        public string UserName { get; set; } =
            Env("LITHOLD_USERNAME", "");
        public string Password { get; set; } =
            Env("LITHOLD_PASSWORD", "");
        public string DataSource { get; set; } =
            Env("LITHOLD_DATASOURCE", "OnBase-QA");

        // Document storage
        public string DocumentTypeName { get; set; } = "CCT - LitigationHold";
        public string FileTypeName { get; set; } = "PDF";

        // Document-level keywords
        public string KeywordCaseNumber { get; set; } = "Case #";
        public string KeywordCaseName { get; set; } = "Case Name";

        public string KeywordMailTo { get; set; } = "MAIL To";
        public string KeywordMailToExt { get; set; } = "MAIL To_ext";
        public string KeywordMailCc { get; set; } = "MAIL Cc";
        public string KeywordMailCcExt { get; set; } = "MAIL Cc_ext";
        public string KeywordMailFrom { get; set; } = "MAIL From";
        public string KeywordMailDate { get; set; } = "MAIL Date";
        public string KeywordMailSubject { get; set; } = "MAIL Subject";

        // Document keywords for URLs
        public string KeywordInstructionUrl { get; set; } = "Case LH Instruction URL";
        public string KeywordCaseFolderUrl { get; set; } = "Case Folder URL";

        // Keyword record type for employee list
        public string EmployeeKeywordRecordTypeName { get; set; } = "CCT - Litigation Employee List";

        // Keyword types within the employee keyword record
        public string KeywordEmployeeName { get; set; } = "Employee Name";
        public string KeywordEmployeeDesignation { get; set; } = "Designation";
        public string KeywordFolderName { get; set; } = "Folder Name";

        public string KeywordEmployeeUserId { get; set; } = "Employee User ID";
        public string KeywordEmployeeEmail { get; set; } = "Employee Email Address";
        public string KeywordEmployeeMailRole { get; set; } = "MAIL"; // FROM/TO/CC/(blank)/SENDER

        private static string Env(string name, string fallback)
        {
            string val = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(val) ? fallback : val;
        }
    }

    public class AdConfig
    {
        public string Domain { get; set; } = "ACG.ARLINGTON.LOCAL";
    }

    // ---------------- AD Helper ----------------
    public class AdUserInfo
    {
        public string UserId { get; set; }      // samAccountName
        public string Email { get; set; }
        public string DisplayName { get; set; }
    }

    public class ActiveDirectoryService
    {
        private readonly AdConfig _config;

        public ActiveDirectoryService(AdConfig config)
        {
            _config = config;
        }

        // Search by DisplayName using PrincipalSearcher
        public AdUserInfo LookupUserByDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            try
            {
                using (var ctx = new PrincipalContext(ContextType.Domain, _config.Domain))
                using (var filter = new UserPrincipal(ctx))
                {
                    filter.DisplayName = displayName.Trim();

                    using (var searcher = new PrincipalSearcher(filter))
                    {
                        var result = searcher.FindOne() as UserPrincipal;
                        if (result == null)
                        {
                            Console.WriteLine("[WARN] AD user not found for display name '{0}'", displayName);
                            return null;
                        }

                        return new AdUserInfo
                        {
                            UserId = result.SamAccountName,
                            Email = result.EmailAddress,
                            DisplayName = result.DisplayName
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] AD lookup failed for '{0}': {1}", displayName, ex.Message);
                return null;
            }
        }

        // Search by EmailAddress using PrincipalSearcher
        public AdUserInfo LookupUserByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            try
            {
                using (var ctx = new PrincipalContext(ContextType.Domain, _config.Domain))
                using (var filter = new UserPrincipal(ctx))
                {
                    filter.EmailAddress = email.Trim();

                    using (var searcher = new PrincipalSearcher(filter))
                    {
                        var result = searcher.FindOne() as UserPrincipal;
                        if (result == null)
                            return null;

                        return new AdUserInfo
                        {
                            UserId = result.SamAccountName,
                            Email = result.EmailAddress,
                            DisplayName = result.DisplayName
                        };
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }

    // ---------------- OnBase Service ----------------
    public class OnBaseService
    {
        private readonly AppConfig _config;
        private readonly ActiveDirectoryService _ad;

        public OnBaseService(AppConfig config, ActiveDirectoryService adService)
        {
            _config = config;
            _ad = adService;
        }

        private Application Connect()
        {
            if (string.IsNullOrWhiteSpace(_config.UserName) || string.IsNullOrWhiteSpace(_config.Password))
                throw new InvalidOperationException(
                    "OnBase credentials not configured. Set LITHOLD_USERNAME and LITHOLD_PASSWORD environment variables.");

            Console.WriteLine("[INFO] Connecting to OnBase at {0}", _config.AppServerUrl);

            AuthenticationProperties authProps =
                Application.CreateOnBaseAuthenticationProperties(
                    _config.AppServerUrl, _config.UserName, _config.Password, _config.DataSource);

            authProps.LicenseType = LicenseType.Default;

            Application app = Application.Connect(authProps);

            Console.WriteLine("[INFO] Connected. Session ID: {0}", app.SessionID);
            Console.WriteLine();

            return app;
        }

        private static string NormalizeName(string name)
            => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

        // Priority: FROM > TO > CC > (blank)
        private static int RolePriority(string role)
        {
            role = (role ?? "").Trim().ToUpperInvariant();
            if (role == "FROM") return 3;
            if (role == "TO") return 2;
            if (role == "CC") return 1;
            return 0;
        }

        private static void SetRoleIfHigher(EmployeeKeywordRecordData rec, string newRole)
        {
            if (rec == null || string.IsNullOrWhiteSpace(newRole)) return;

            var current = (rec.MailRole ?? "").Trim();
            if (RolePriority(newRole) > RolePriority(current))
                rec.MailRole = newRole.Trim().ToUpperInvariant();
        }

        public long CreateLitigationHoldDocument(LitigationHold hold, string pdfFilePath, int caseNumber)
        {
            if (hold == null) throw new ArgumentNullException(nameof(hold));
            if (string.IsNullOrWhiteSpace(pdfFilePath) || !File.Exists(pdfFilePath))
                throw new FileNotFoundException("PDF file not found.", pdfFilePath);

            // Cross-fill From and CaseSentBy: if one has no Name, copy from the other
            bool fromHasName = hold.From != null && !string.IsNullOrWhiteSpace(hold.From.Name);
            bool sentByHasName = hold.CaseSentBy != null && !string.IsNullOrWhiteSpace(hold.CaseSentBy.Name);

            if (!fromHasName && sentByHasName)
            {
                hold.From = new Person { Name = hold.CaseSentBy.Name.Trim(), Designation = hold.CaseSentBy.Designation };
            }
            else if (!sentByHasName && fromHasName)
            {
                hold.CaseSentBy = new Person { Name = hold.From.Name.Trim(), Designation = hold.From.Designation };
            }

            // Case Name from Re: "Litigation Hold: Olga Sokolov" -> "Olga Sokolov"
            string caseName = DeriveCaseName(hold.Re, caseNumber);

            // MAIL Subject
            string mailSubject = "Confidential - Litigation Hold - " + caseName;

            // Mail date
            DateTime mailDate = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(hold.Date) && DateTime.TryParse(hold.Date, out var parsed))
                mailDate = parsed;

            // Build unified employee map across Employees + To + CC + From
            var employeeMap = BuildUnifiedEmployeeMap(hold);

            // AD enrichment
            foreach (var emp in employeeMap.Values)
            {
                var ad = _ad.LookupUserByDisplayName(emp.Name);
                if (ad != null)
                {
                    emp.UserId = ad.UserId;
                    emp.Email = ad.Email;
                }
            }

            // Add separate SENDER record based on SenderEmailID (MAIL = SENDER), with real AD DisplayName
            AddSenderEmployeeRecord(employeeMap, hold);

            // Document-level EmployeeID rollups (concatenated with ";"):
            //   - "MAIL To" = ordered: FROM first, then CC, then SENDER, then all other case users
            //   - "MAIL Cc" = EmployeeIDs where employee record MAIL role is TO
            // If the concatenated string exceeds 250 chars, overflow goes to MAIL To_ext / MAIL Cc_ext
            var mailToOrdered = new List<string>();
            var mailCcOrdered = new List<string>();
            var mailToSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mailCcSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Helper to append UserId to list in order, skipping duplicates
            Action<List<string>, HashSet<string>, string> addUnique = (list, seen, id) =>
            {
                if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                    list.Add(id);
            };

            // Pass 1: FROM
            foreach (var emp in employeeMap.Values)
            {
                if ((emp.MailRole ?? "").Trim().ToUpperInvariant() == "FROM"
                    && !string.IsNullOrWhiteSpace(emp.UserId))
                    addUnique(mailToOrdered, mailToSeen, emp.UserId.Trim());
            }

            // Pass 2: CC
            foreach (var emp in employeeMap.Values)
            {
                if ((emp.MailRole ?? "").Trim().ToUpperInvariant() == "CC"
                    && !string.IsNullOrWhiteSpace(emp.UserId))
                    addUnique(mailToOrdered, mailToSeen, emp.UserId.Trim());
            }

            // Pass 3: SENDER
            foreach (var emp in employeeMap.Values)
            {
                if ((emp.MailRole ?? "").Trim().ToUpperInvariant() == "SENDER"
                    && !string.IsNullOrWhiteSpace(emp.UserId))
                    addUnique(mailToOrdered, mailToSeen, emp.UserId.Trim());
            }

            // Pass 4: all remaining case users (blank role / any other role except TO)
            foreach (var emp in employeeMap.Values)
            {
                var role = (emp.MailRole ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(emp.UserId)) continue;

                if (role == "TO")
                    addUnique(mailCcOrdered, mailCcSeen, emp.UserId.Trim());
                else
                    addUnique(mailToOrdered, mailToSeen, emp.UserId.Trim());
            }

            // Split at 250 chars: primary keyword gets first 250, overflow goes to _ext
            // Trailing ";" after the last EmployeeID in each segment
            string mailToFull = mailToOrdered.Count > 0 ? string.Join(";", mailToOrdered) + ";" : "";
            string mailToValue;
            string mailToExtValue;
            SplitAt250(mailToFull, out mailToValue, out mailToExtValue);

            string mailCcFull = mailCcOrdered.Count > 0 ? string.Join(";", mailCcOrdered) + ";" : "";
            string mailCcValue;
            string mailCcExtValue;
            SplitAt250(mailCcFull, out mailCcValue, out mailCcExtValue);

            // URLs
            string instructionUrl = BuildInstructionUrl(caseNumber, caseName, hold.From?.Name, mailDate);
            string caseFolderUrl = BuildCaseFolderUnc(caseNumber);

            // Store in OnBase
            using (var app = Connect())
            {
                Console.WriteLine("[INFO] Attempting to archive Litigation Hold document...");

                DocumentType docType = app.Core.DocumentTypes.Find(_config.DocumentTypeName);
                if (docType == null) throw new Exception($"Document type '{_config.DocumentTypeName}' not found.");

                FileType fileType = app.Core.FileTypes.Find(_config.FileTypeName);
                if (fileType == null) throw new Exception($"File type '{_config.FileTypeName}' not found.");

                StoreNewDocumentProperties props = app.Core.Storage.CreateStoreNewDocumentProperties(docType, fileType);

                // Document-level keywords
                SafeAddKeyword(props, _config.KeywordCaseNumber, caseNumber.ToString());
                SafeAddKeyword(props, _config.KeywordCaseName, caseName);

                SafeAddKeyword(props, _config.KeywordMailFrom, hold.From?.Name);
                SafeAddKeyword(props, _config.KeywordMailDate, mailDate);
                SafeAddKeyword(props, _config.KeywordMailSubject, mailSubject);

                SafeAddKeyword(props, _config.KeywordMailTo, mailToValue);
                SafeAddKeyword(props, _config.KeywordMailToExt, mailToExtValue);
                SafeAddKeyword(props, _config.KeywordMailCc, mailCcValue);
                SafeAddKeyword(props, _config.KeywordMailCcExt, mailCcExtValue);

                SafeAddKeyword(props, _config.KeywordInstructionUrl, instructionUrl);
                SafeAddKeyword(props, _config.KeywordCaseFolderUrl, caseFolderUrl);

                props.DocumentDate = mailDate;

                // Store the PDF
                var files = new List<string> { pdfFilePath };
                Document newDoc = app.Core.Storage.StoreNewDocument(files, props);

                Console.WriteLine("[INFO] Document import successful. New Document ID: {0}", newDoc.ID);

                // Add employee keyword records
                AddEmployeeKeywordRecords(app, newDoc, employeeMap.Values.ToList());

                Console.WriteLine("[INFO] Employee keyword records added to document {0}.", newDoc.ID);

                return newDoc.ID;
            }
        }

        private void AddSenderEmployeeRecord(Dictionary<string, EmployeeKeywordRecordData> employeeMap, LitigationHold hold)
        {
            if (employeeMap == null || hold == null)
                return;

            if (string.IsNullOrWhiteSpace(hold.SenderEmailID))
                return;

            string senderEmail = hold.SenderEmailID.Trim();
            string senderKey = "SENDER_EMAIL::" + senderEmail.ToLowerInvariant();

            if (employeeMap.ContainsKey(senderKey))
                return;

            // Fallback if AD lookup fails
            string fallback = senderEmail.Contains("@")
                ? senderEmail.Split('@')[0]
                : senderEmail;

            string displayName = null;
            string userId = null;
            string email = senderEmail;

            // Prefer real AD display name for sender, based on email
            var ad = _ad.LookupUserByEmail(senderEmail);
            if (ad != null)
            {
                displayName = ad.DisplayName;
                userId = ad.UserId;
                if (!string.IsNullOrWhiteSpace(ad.Email)) email = ad.Email;
            }

            var senderRec = new EmployeeKeywordRecordData
            {
                Name = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName,
                Designation = null,
                FolderName = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName,
                MailRole = "SENDER",
                Email = email,
                UserId = userId
            };

            employeeMap[senderKey] = senderRec;
        }

        private static string DeriveCaseName(string re, int caseNumber)
        {
            if (string.IsNullOrWhiteSpace(re))
                return caseNumber.ToString();

            var tmp = re.Trim();
            int colon = tmp.IndexOf(':');
            if (colon >= 0 && colon + 1 < tmp.Length)
            {
                var after = tmp.Substring(colon + 1).Trim();
                return string.IsNullOrWhiteSpace(after) ? caseNumber.ToString() : after;
            }

            return tmp;
        }

        private static string BuildInstructionUrl(int caseNumber, string caseName, string sentBy, DateTime sentDate)
        {
            string sentDateStr = sentDate.ToString("M/d/yyyy");

            return
                "http://prd-web2-vip.arlingtonva.us/litigationhold/instructions.html" +
                "?casenum=" + Uri.EscapeDataString(caseNumber.ToString()) +
                "&sentby=" + Uri.EscapeDataString(sentBy ?? "") +
                "&sentdate=" + Uri.EscapeDataString(sentDateStr) +
                "&casename=" + Uri.EscapeDataString(caseName ?? "");
        }

        private static string BuildCaseFolderUnc(int caseNumber)
        {
            return @"\\acg.arlington.local\ArlGov\ACG-Inter-Dept-Shares\ACG-LH\" + caseNumber + @"\";
        }

        private Dictionary<string, EmployeeKeywordRecordData> BuildUnifiedEmployeeMap(LitigationHold hold)
        {
            var map = new Dictionary<string, EmployeeKeywordRecordData>(StringComparer.OrdinalIgnoreCase);

            EmployeeKeywordRecordData Upsert(Person p)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Name)) return null;
                string key = NormalizeName(p.Name);

                if (!map.TryGetValue(key, out var rec))
                {
                    rec = new EmployeeKeywordRecordData
                    {
                        Name = p.Name,
                        Designation = p.Designation,
                        FolderName = p.Name,
                        MailRole = null
                    };
                    map[key] = rec;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(rec.Designation) && !string.IsNullOrWhiteSpace(p.Designation))
                        rec.Designation = p.Designation;
                }
                return rec;
            }

            // Employees list (role blank)
            if (hold.Employees != null)
            {
                foreach (var p in hold.Employees)
                    Upsert(p);
            }

            // FROM
            if (hold.From != null)
            {
                var rec = Upsert(hold.From);
                if (rec != null) SetRoleIfHigher(rec, "FROM");
            }

            // TO
            if (hold.To != null)
            {
                foreach (var p in hold.To)
                {
                    var rec = Upsert(p);
                    if (rec != null) SetRoleIfHigher(rec, "TO");
                }
            }

            // CC
            if (hold.CC != null)
            {
                foreach (var p in hold.CC)
                {
                    var rec = Upsert(p);
                    if (rec != null) SetRoleIfHigher(rec, "CC");
                }
            }

            return map;
        }

        private void AddEmployeeKeywordRecords(Application app, Document document, List<EmployeeKeywordRecordData> employees)
        {
            var recordType = app.Core.KeywordRecordTypes.Find(_config.EmployeeKeywordRecordTypeName);
            if (recordType == null)
            {
                Console.WriteLine("[WARN] Keyword record type '{0}' not found; skipping employee keyword records.",
                    _config.EmployeeKeywordRecordTypeName);
                return;
            }

            // Keyword types used inside record
            KeywordType nameType = app.Core.KeywordTypes.Find(_config.KeywordEmployeeName);
            KeywordType desigType = app.Core.KeywordTypes.Find(_config.KeywordEmployeeDesignation);
            KeywordType folderType = app.Core.KeywordTypes.Find(_config.KeywordFolderName);
            KeywordType userIdType = app.Core.KeywordTypes.Find(_config.KeywordEmployeeUserId);
            KeywordType emailType = app.Core.KeywordTypes.Find(_config.KeywordEmployeeEmail);
            KeywordType mailRoleType = app.Core.KeywordTypes.Find(_config.KeywordEmployeeMailRole);

            if (nameType == null || desigType == null || folderType == null)
            {
                Console.WriteLine("[WARN] Required employee keyword types (Name/Designation/Folder) not found; skipping records.");
                return;
            }

            using (DocumentLock docLock = document.LockDocument())
            {
                if (docLock.Status != DocumentLockStatus.LockObtained)
                {
                    Console.WriteLine("[WARN] Could not lock document {0} for keyword record update.", document.ID);
                    return;
                }

                KeywordModifier mod = document.CreateKeywordModifier();

                foreach (var emp in employees)
                {
                    EditableKeywordRecord rec = recordType.CreateEditableKeywordRecord();

                    SafeAddRecordKeyword(rec, nameType, emp.Name, "Employee Name");
                    SafeAddRecordKeyword(rec, desigType, emp.Designation, "Designation");
                    SafeAddRecordKeyword(rec, folderType,
                        string.IsNullOrWhiteSpace(emp.FolderName) ? emp.Name : emp.FolderName,
                        "Folder Name");

                    if (userIdType != null && !string.IsNullOrWhiteSpace(emp.UserId))
                        SafeAddRecordKeyword(rec, userIdType, emp.UserId, "Employee User ID");

                    if (emailType != null && !string.IsNullOrWhiteSpace(emp.Email))
                        SafeAddRecordKeyword(rec, emailType, emp.Email, "Employee Email Address");

                    if (mailRoleType != null && !string.IsNullOrWhiteSpace(emp.MailRole))
                        SafeAddRecordKeyword(rec, mailRoleType, emp.MailRole, "MAIL");

                    mod.AddKeywordRecord(rec);
                }

                mod.ApplyChanges();
            }
        }

        // Split a ";"-delimited string at the 250-char boundary without cutting an ID in half.
        // Both primary and overflow end with ";" (e.g. "JSMITH;MJONES;" and "VSINHA;KLEE;").
        private static void SplitAt250(string full, out string primary, out string overflow)
        {
            const int limit = 250;
            overflow = null;

            if (string.IsNullOrEmpty(full) || full.Length <= limit)
            {
                primary = full;
                return;
            }

            // Find the last ";" at or before position 250 so we don't split mid-ID
            int cut = full.LastIndexOf(';', limit - 1);
            if (cut <= 0)
            {
                // No semicolon found before 250 — take the whole thing as primary
                primary = full;
                return;
            }

            // Include the ";" in primary: "JSMITH;MJONES;"
            primary = full.Substring(0, cut + 1);
            overflow = full.Substring(cut + 1);

            // overflow is empty if nothing remains after the cut
            if (string.IsNullOrWhiteSpace(overflow))
                overflow = null;
        }

        private static void SafeAddKeyword(StoreNewDocumentProperties props, string keywordName, string value)
        {
            if (props == null || string.IsNullOrWhiteSpace(keywordName) || string.IsNullOrWhiteSpace(value))
                return;

            try
            {
                props.AddKeyword(keywordName, value.Trim());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] Failed to add STRING keyword '{0}' = '{1}': {2}",
                    keywordName, value, ex.Message);
            }
        }

        private static void SafeAddKeyword(StoreNewDocumentProperties props, string keywordName, DateTime value)
        {
            if (props == null || string.IsNullOrWhiteSpace(keywordName))
                return;

            try
            {
                props.AddKeyword(keywordName, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] Failed to add DATE keyword '{0}' = {1}: {2}",
                    keywordName, value.ToString("o"), ex.Message);
            }
        }

        private static void SafeAddRecordKeyword(EditableKeywordRecord record, KeywordType type, string value, string label)
        {
            if (record == null || type == null || string.IsNullOrWhiteSpace(value))
                return;

            try
            {
                var kw = type.CreateKeyword(value.Trim());
                record.AddKeyword(kw);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] Failed to add keyword record '{0}' value '{1}': {2}", label, value, ex.Message);
            }
        }
    }

    // ---------------- Program Entry ----------------
    public class Program
    {
        private static readonly object _counterLock = new object();

        private static int GetNextCaseNumber(string counterFile)
        {
            try
            {
                if (!File.Exists(counterFile))
                {
                    File.WriteAllText(counterFile, "1");
                    return 1;
                }

                string txt = File.ReadAllText(counterFile).Trim();
                if (int.TryParse(txt, out var val) && val > 0)
                    return val;

                File.WriteAllText(counterFile, "1");
                return 1;
            }
            catch
            {
                return 1;
            }
        }

        private static void SaveNextCaseNumber(string counterFile, int next)
        {
            try { File.WriteAllText(counterFile, next.ToString()); }
            catch (Exception ex) { Console.WriteLine("[WARN] Failed to write case counter file: {0}", ex.Message); }
        }

        public static int Main(string[] args)
        {
            var config = new AppConfig();
            var adConfig = new AdConfig();

            Directory.CreateDirectory(config.IncomingFolder);
            Directory.CreateDirectory(config.ProcessedFolder);
            Directory.CreateDirectory(config.ErrorFolder);

            Console.WriteLine("[INFO] Incoming folder : {0}", config.IncomingFolder);
            Console.WriteLine("[INFO] Processed folder: {0}", config.ProcessedFolder);
            Console.WriteLine("[INFO] Error folder    : {0}", config.ErrorFolder);

            var ad = new ActiveDirectoryService(adConfig);
            var onbase = new OnBaseService(config, ad);

            var jsonFiles = Directory.GetFiles(config.IncomingFolder, "*.json");
            if (jsonFiles.Length == 0)
                return 0;

            foreach (var jsonPath in jsonFiles)
            {
                Console.WriteLine("[INFO] Processing file: {0}", Path.GetFileName(jsonPath));

                try
                {
                    string json = File.ReadAllText(jsonPath);
                    var req = JsonConvert.DeserializeObject<LitigationHoldRequest>(json);

                    if (req?.LitigationHold == null)
                        throw new Exception("Missing LitigationHold object in JSON.");

                    // Expect PDF with same base name
                    string pdfPath = Path.ChangeExtension(jsonPath, ".pdf");
                    if (!File.Exists(pdfPath))
                    {
                        string alt = Path.ChangeExtension(jsonPath, ".PDF");
                        if (File.Exists(alt)) pdfPath = alt;
                        else throw new FileNotFoundException("Matching PDF not found for JSON.", jsonPath);
                    }

                    int caseNum;
                    lock (_counterLock)
                    {
                        caseNum = GetNextCaseNumber(config.CaseCounterFile);
                        SaveNextCaseNumber(config.CaseCounterFile, caseNum + 1);
                    }

                    long docId = onbase.CreateLitigationHoldDocument(req.LitigationHold, pdfPath, caseNum);

                    Console.WriteLine("[INFO] Next Case # saved as {0}.", caseNum + 1);
                    Console.WriteLine("[INFO] OnBase document created: ID = {0}", docId);

                    // Move JSON and PDF to processed
                    string destJson = Path.Combine(config.ProcessedFolder, Path.GetFileName(jsonPath));
                    string destPdf = Path.Combine(config.ProcessedFolder, Path.GetFileName(pdfPath));

                    if (File.Exists(destJson)) File.Delete(destJson);
                    if (File.Exists(destPdf)) File.Delete(destPdf);

                    File.Move(jsonPath, destJson);
                    File.Move(pdfPath, destPdf);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] Failed to process file {0}: {1}", jsonPath, ex);

                    try
                    {
                        string destJson = Path.Combine(config.ErrorFolder, Path.GetFileName(jsonPath));
                        if (File.Exists(destJson)) File.Delete(destJson);
                        if (File.Exists(jsonPath)) File.Move(jsonPath, destJson);
                    }
                    catch { }

                    try
                    {
                        string pdfPath = Path.ChangeExtension(jsonPath, ".pdf");
                        string alt = Path.ChangeExtension(jsonPath, ".PDF");
                        string pdfToMove = File.Exists(pdfPath) ? pdfPath : (File.Exists(alt) ? alt : null);

                        if (pdfToMove != null)
                        {
                            string destPdf = Path.Combine(config.ErrorFolder, Path.GetFileName(pdfToMove));
                            if (File.Exists(destPdf)) File.Delete(destPdf);
                            File.Move(pdfToMove, destPdf);
                        }
                    }
                    catch { }
                }
            }

            return 0;
        }
    }
}
