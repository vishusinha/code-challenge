// File: LitigationHoldCreateFolders.cs
// Notes: C# 7.3 safe; Domain/RootFolder come from Configuration Items.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Security.Principal;
using Hyland.Unity;
using Hyland.Unity.Workflow;

namespace CAO.LitigationHold
{
    public sealed class LitigationHoldCreateFolders : IWorkflowScript
    {
        // PropertyBag keys
        private const string PROP_FILEPATH      = "propFilepath";
        private const string PROP_FILEPATH_ALT1 = "propFilePath";
        private const string PROP_FILEPATH_ALT2 = "FormXmlPath";
        private const string PROP_FILEPATH_ALT3 = "xmlPath";
        private const string PROP_NOACL         = "propNoAcl";
        private const string PROP_ERROR_MESSAGE = "propErrorMessage";

        // AppServer Configuration Item keys
        private const string CFG_DOMAIN     = "CAO_LitHold_Domain";
        private const string CFG_PSW        = "CAO_Script_psw";
        private const string CFG_USERNAME   = "CAO_Script_Username";
        private const string CFG_ROOTFOLDER = "CAO_LitHold_Root_Folder";

        private const string SCRIPT_NAME = "LitigationHoldCreateFolders";

        public void OnWorkflowScriptExecute(Application app, WorkflowEventArgs args)
        {
            args.ScriptResult = false;

            try
            {
                app.Diagnostics.Level = Diagnostics.DiagnosticsLevel.Verbose;
                LogDual(app, args, DefaultDiagLogFolder(), WorkflowPropertyBagHelper.Dump(args.PropertyBag));

                // Resolve XML path
                string xmlPath;
                bool got = WorkflowPropertyBagHelper.GetString(args.PropertyBag, PROP_FILEPATH, out xmlPath)
                        || WorkflowPropertyBagHelper.GetString(args.PropertyBag, PROP_FILEPATH_ALT1, out xmlPath)
                        || WorkflowPropertyBagHelper.GetString(args.PropertyBag, PROP_FILEPATH_ALT2, out xmlPath)
                        || WorkflowPropertyBagHelper.GetString(args.PropertyBag, PROP_FILEPATH_ALT3, out xmlPath);
                if (!got) { FailDual(app, args, "Missing propFilepath/propFilePath/FormXmlPath/xmlPath."); return; }
                LogDual(app, args, DefaultDiagLogFolder(), "Resolved XML path: " + xmlPath);
                if (!File.Exists(xmlPath)) { FailDual(app, args, "Form XML not found: " + xmlPath); return; }

                // Parse XML into settings
                Settings s = Settings.LoadFromUnityFormXml(xmlPath);

                // Domain strictly from Configuration Items
                string cfgDomain;
                if (!TryGetConfigString(app, CFG_DOMAIN, out cfgDomain) || string.IsNullOrWhiteSpace(cfgDomain))
                    throw new Exception(CFG_DOMAIN + " not a Configuration Item");
                s.Domain = cfgDomain.Trim();

                // Password from Configuration Items
                string cfgPassword;
                if (!TryGetConfigString(app, CFG_PSW, out cfgPassword) || string.IsNullOrWhiteSpace(cfgPassword))
                    throw new Exception(CFG_PSW + " not a Configuration Item");

                // Service account username from Configuration Items
                string cfgUsername;
                if (!TryGetConfigString(app, CFG_USERNAME, out cfgUsername) || string.IsNullOrWhiteSpace(cfgUsername))
                    throw new Exception(CFG_USERNAME + " not a Configuration Item");

                // Store credentials in Settings for use by icacls execution
                s.ServiceUsername = cfgUsername.Trim();
                s.ServicePassword = cfgPassword.Trim();

                // RootFolder from Configuration Items
                string cfgRoot;
                if (!TryGetConfigString(app, CFG_ROOTFOLDER, out cfgRoot) || string.IsNullOrWhiteSpace(cfgRoot))
                    throw new Exception(CFG_ROOTFOLDER + " not a Configuration Item");
                s.RootFolder = cfgRoot.Trim();

                if (string.IsNullOrWhiteSpace(s.LogFolder))      s.LogFolder      = Path.Combine(s.RootFolder, "_Logs");
                if (string.IsNullOrWhiteSpace(s.AutoFillFolder)) s.AutoFillFolder = Path.Combine(s.RootFolder, "_AutoFill");

                bool noAcl = GetBool(args.PropertyBag, PROP_NOACL, false);

                LogDual(app, args, s.LogFolder,
                    SCRIPT_NAME + " START Case=" + s.CaseNumber + " Name='" + s.CaseName + "' Persons=" + s.Persons.Count +
                    " Sources=" + s.Sources.Count + " Domain='" + s.Domain + "' Root='" + s.RootFolder + "' noAcl=" + noAcl);

                // Create folder tree
                CreateStructure(app, args, s);

                // Permissions
                if (!noAcl) ApplyPermissions(app, args, s);
                else LogDual(app, args, s.LogFolder, "ACL: Skipped (propNoAcl=true)");

                // AutoFill file
                CreateAutoFillFile(app, args, s.AutoFillFolder, s.CaseNumber, s.CaseName);

                LogDual(app, args, s.LogFolder, SCRIPT_NAME + " DONE " + s.CaseNumber);
                args.ScriptResult = true;
            }
            catch (UnityAPIException uae) { FailDual(app, args, "Unity exception: " + uae.Message); }
            catch (Exception ex) { FailDual(app, args, ex.Message); }
        }

        // --- Configuration Item access ---
        private static bool TryGetConfigString(Application app, string key, out string value)
        {
            value = null;
            try
            {
                string tmp;
                if (app.Configuration != null && app.Configuration.TryGetValue(key, out tmp))
                {
                    value = string.IsNullOrWhiteSpace(tmp) ? null : tmp.Trim();
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
            catch { }
            return false;
        }

        // ---------- Folder creation ----------
        private static void CreateStructure(Application app, WorkflowEventArgs args, Settings s)
        {
            string caseFolder = Path.Combine(s.RootFolder, s.CaseNumber);
            Directory.CreateDirectory(caseFolder);
            LogDual(app, args, s.LogFolder, "MKDIR " + caseFolder);

            for (int i = 0; i < s.Persons.Count; i++)
            {
                PersonSpec p = s.Persons[i];
                string personFolder = Path.Combine(caseFolder, SanPart(p.PersonFolderName));
                Directory.CreateDirectory(personFolder);
                LogDual(app, args, s.LogFolder, "MKDIR " + personFolder);

                for (int j = 0; j < s.Sources.Count; j++)
                {
                    string srcName = SanPart(s.Sources[j]);
                    string srcFolder = Path.Combine(personFolder, srcName);
                    Directory.CreateDirectory(srcFolder);
                    LogDual(app, args, s.LogFolder, "MKDIR " + srcFolder);

                    if (!string.IsNullOrWhiteSpace(s.OnBaseSub))
                    {
                        string sub = Path.Combine(srcFolder, s.OnBaseSub);
                        Directory.CreateDirectory(sub);
                        LogDual(app, args, s.LogFolder, "MKDIR " + sub);
                    }
                }
            }
        }

        // ---------- Permissions (icacls) ----------
        // Each contributor gets RW only on their own person folder (+ sources/sub beneath it).
        // A single recursive /T grant on the person folder covers all children,
        // so we do NOT grant at the case-root level (that would leak access across persons).
        private static void ApplyPermissions(Application app, WorkflowEventArgs args, Settings s)
        {
            if (s.Persons.Count == 0) { LogDual(app, args, s.LogFolder, "ACL: No persons to process."); return; }

            string caseFolder = Path.Combine(s.RootFolder, s.CaseNumber);
            List<string> normPersonPerms = NormalizePerms(s.PersonPermissions);
            List<string> normSourcePerms = NormalizePerms(s.SourcePermissions);

            // Default to Modify if XML didn't provide perms
            if (normPersonPerms.Count == 0)
            {
                normPersonPerms.Add("(OI)(CI)M");
                LogDual(app, args, s.LogFolder, "ACL WARN: PersonPermissions missing in XML. Defaulting to (OI)(CI)M");
            }

            if (normSourcePerms.Count == 0)
            {
                normSourcePerms.Add("(OI)(CI)M");
                LogDual(app, args, s.LogFolder, "ACL WARN: SourcePermissions missing in XML. Defaulting to (OI)(CI)M");
            }

            // Track users already granted case-folder Read to avoid duplicate icacls calls
            List<string> caseFolderGranted = new List<string>();

            for (int i = 0; i < s.Persons.Count; i++)
            {
                PersonSpec p = s.Persons[i];
                string personFolder = Path.Combine(caseFolder, SanPart(p.PersonFolderName));

                for (int u = 0; u < p.Contributors.Count; u++)
                {
                    string user = DomainUser(s.Domain, p.Contributors[u]);

                    // Grant non-inheriting Read on the case folder so the user can browse
                    // to their person folder. No (OI)(CI) = "this folder only" — does NOT
                    // leak into other person folders.
                    if (!ContainsIgnoreCase(caseFolderGranted, user))
                    {
                        LogDual(app, args, s.LogFolder,
                            "ACL GRANT (case folder list-only) Folder=" + caseFolder +
                            " User=" + user + " Perm=R (this folder only)");

                        string listArg = "/grant " + Q(user) + ":R";
                        RunIcacls(app, args, s, caseFolder, listArg);
                        caseFolderGranted.Add(user);
                    }

                    // Clean slate: remove any pre-existing ACE for this user on their person folder
                    TryRemoveAce(app, args, s, personFolder, user);

                    // Single recursive grant on the person folder — inherits into all source/sub folders
                    for (int k = 0; k < normPersonPerms.Count; k++)
                    {
                        LogDual(app, args, s.LogFolder,
                            "ACL GRANT (person folder recursive) Folder=" + personFolder +
                            " User=" + user + " Perm=" + normPersonPerms[k]);

                        string arg = "/grant " + Q(user) + ":" + normPersonPerms[k] + " /T";
                        RunIcacls(app, args, s, personFolder, arg);
                    }

                    // If source-level perms differ from person-level, apply them explicitly
                    if (!PermsEqual(normSourcePerms, normPersonPerms))
                    {
                        for (int j = 0; j < s.Sources.Count; j++)
                        {
                            string srcFolder = Path.Combine(personFolder, SanPart(s.Sources[j]));
                            for (int sp = 0; sp < normSourcePerms.Count; sp++)
                            {
                                LogDual(app, args, s.LogFolder,
                                    "ACL GRANT (source folder) Folder=" + srcFolder +
                                    " User=" + user + " Perm=" + normSourcePerms[sp]);

                                string arg = "/grant " + Q(user) + ":" + normSourcePerms[sp] + " /T";
                                RunIcacls(app, args, s, srcFolder, arg);
                            }
                        }
                    }
                }
            }

            LogDual(app, args, s.LogFolder, "ACL: Completed.");
        }

        private static bool PermsEqual(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        // Valid tokens: F,M,RX,R,W (tolerant of "R W","R,W","RW")
        private static List<string> NormalizePerms(List<string> raw)
        {
            List<string> list = new List<string>();
            if (raw == null) return list;
            for (int i = 0; i < raw.Count; i++)
            {
                string t = (raw[i] ?? "").Trim().ToUpperInvariant();
                if (t.Length == 0) continue;

                if (t == "RW" || t == "WR") { AddOnce(list, "(OI)(CI)R"); AddOnce(list, "(OI)(CI)W"); continue; }
                if (t == "X" || t == "E") t = "RX";

                if (t == "F" || t == "M" || t == "RX" || t == "R" || t == "W")
                {
                    AddOnce(list, "(OI)(CI)" + t);
                    continue;
                }

                string[] parts = t.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int p = 0; p < parts.Length; p++)
                {
                    string z = parts[p];
                    if (z == "F" || z == "M" || z == "RX" || z == "R" || z == "W")
                        AddOnce(list, "(OI)(CI)" + z);
                }
            }
            return list;
        }

        private static void AddOnce(List<string> list, string val)
        {
            for (int i = 0; i < list.Count; i++) if (string.Equals(list[i], val, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(val);
        }

        // Resolve DOMAIN\user -> SID via NTAccount (no DirectoryServices needed)
        private static string ResolveSidFromDomainUser(string domainUser)
        {
            try
            {
                NTAccount nt = new NTAccount(domainUser);
                SecurityIdentifier sid = (SecurityIdentifier)nt.Translate(typeof(SecurityIdentifier));
                return sid.Value;
            }
            catch
            {
                return null;
            }
        }

        // Safe remove if ACE present
        private static void TryRemoveAce(Application app, WorkflowEventArgs args, Settings s, string target, string domainUser)
        {
            string userQ = Q(domainUser);
            LogDual(app, args, s.LogFolder, "TryRemoveAce target=" + Q(target) + " user=" + userQ);

            string sid = ResolveSidFromDomainUser(domainUser);
            if (string.IsNullOrWhiteSpace(sid))
            {
                LogDual(app, args, s.LogFolder, "ICACLS WARN: Could not resolve SID for " + domainUser + " (skipping /remove check).");
                return;
            }

            LogDual(app, args, s.LogFolder, "ICACLS SID RESOLVED: " + domainUser + " => " + sid);

            // Dump current ACL and check for either the SID or DOMAIN\User
            Tuple<int, string, string> dump = ExecIcacls(app, args, s, target, "");
            string dumpText = (dump.Item2 ?? "") + "\n" + (dump.Item3 ?? "");

            bool present =
                dumpText.IndexOf(sid, StringComparison.OrdinalIgnoreCase) >= 0 ||
                dumpText.IndexOf(domainUser, StringComparison.OrdinalIgnoreCase) >= 0;

            if (!present)
            {
                LogDual(app, args, s.LogFolder,
                    ("ICACLS SKIP /remove (ACE not found) target=" + Q(target) +
                     " user=" + userQ + " sid=" + sid).Trim());
                return;
            }

            // Remove by account name
            Tuple<int, string, string> rem = ExecIcacls(app, args, s, target, "/remove " + userQ);
            if (rem.Item1 != 0)
            {
                LogDual(app, args, s.LogFolder, "ICACLS ERROR [" + rem.Item1 + "] /remove " + Q(target) + " " + userQ + "\nSTDERR: " + rem.Item3 + "\nSTDOUT: " + rem.Item2);
                throw new InvalidOperationException("icacls failed: " + Q(target) + " /remove " + userQ);
            }

            LogDual(app, args, s.LogFolder, ("ICACLS OK /remove " + Q(target) + " " + userQ + "\n" + (rem.Item2 ?? "")).Trim());
        }

        private static System.Security.SecureString ToSecureString(string s)
        {
            var ss = new System.Security.SecureString();
            if (!string.IsNullOrEmpty(s))
            {
                foreach (char c in s) ss.AppendChar(c);
            }
            ss.MakeReadOnly();
            return ss;
        }

        private static void RunIcacls(Application app, WorkflowEventArgs args, Settings s, string target, string argsLine)
        {
            if (!Directory.Exists(target) && !File.Exists(target))
                throw new DirectoryNotFoundException("Target not found: " + target);

            Tuple<int, string, string> r = ExecIcacls(app, args, s, target, argsLine);

            if (r.Item1 == 5)
            {
                LogDual(app, args, s.LogFolder,
                    "ICACLS ERROR [5] " + Q(target) + " " + argsLine + "\n" +
                    "HINT: Workflow service account needs 'Change permissions' on the parent/root.");
                throw new UnauthorizedAccessException("icacls denied: " + Q(target));
            }

            if (r.Item1 != 0)
            {
                LogDual(app, args, s.LogFolder, "ICACLS ERROR [" + r.Item1 + "] " + Q(target) + " " + argsLine + "\nSTDERR: " + r.Item3 + "\nSTDOUT: " + r.Item2);
                throw new InvalidOperationException("icacls failed: " + Q(target) + " " + argsLine);
            }

            if (!string.IsNullOrEmpty(r.Item2))
                LogDual(app, args, s.LogFolder, "ICACLS OK " + Q(target) + " " + argsLine + "\n" + r.Item2.Trim());
            else
                LogDual(app, args, s.LogFolder, "ICACLS OK " + Q(target) + " " + argsLine);
        }

        private static Tuple<int, string, string> ExecIcacls(Application app, WorkflowEventArgs args, Settings s, string target, string argsLine)
        {
            LogDual(app, args, s.LogFolder, "ICACLS argsLine = " + argsLine);

            string fullArgs = Q(target) + " " + argsLine;

            var psi = new ProcessStartInfo
            {
                FileName = "icacls.exe",
                Arguments = fullArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Domain = s.Domain,
                UserName = s.ServiceUsername,
                Password = ToSecureString(s.ServicePassword),
                WindowStyle = ProcessWindowStyle.Hidden
            };

            LogDual(app, args, s.LogFolder, "ICACLS EXEC FileName=" + psi.FileName);
            LogDual(app, args, s.LogFolder, "ICACLS EXEC Arguments=" + psi.Arguments);

            try
            {
                using (Process p = Process.Start(psi))
                {
                    if (p == null)
                        throw new InvalidOperationException("Process.Start returned null.");

                    LogDual(app, args, s.LogFolder, "ICACLS Process started. PID=" + p.Id);

                    // Wait up to 5 minutes
                    bool exited = p.WaitForExit(300000);
                    if (!exited)
                    {
                        try
                        {
                            LogDual(app, args, s.LogFolder, "ICACLS TIMEOUT (5 min). Killing PID=" + p.Id);
                            p.Kill();
                        }
                        catch (Exception kex)
                        {
                            LogDual(app, args, s.LogFolder, "ICACLS Kill failed: " + kex.Message);
                        }

                        return Tuple.Create(1460, "", "ICACLS timed out after 5 minutes.");
                    }

                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();

                    LogDual(app, args, s.LogFolder, "ICACLS EXIT code=" + p.ExitCode);

                    return Tuple.Create(p.ExitCode, stdout, stderr);
                }
            }
            catch (Exception ex)
            {
                LogDual(app, args, s.LogFolder,
                    "ICACLS EXCEPTION: " + ex.GetType().FullName + " | " + ex.Message);

                return Tuple.Create(999, "", ex.ToString());
            }
        }

        // ---------- AutoFill ----------
        private static void CreateAutoFillFile(Application app, WorkflowEventArgs args, string folder, string caseNumber, string caseName)
        {
            if (string.IsNullOrWhiteSpace(folder))
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LitigationHold", "AutoFill");

            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, SanPart(caseNumber) + "_autofill.txt");
            File.WriteAllText(path, (caseNumber ?? string.Empty) + "*" + (caseName ?? string.Empty));
            LogDual(app, args, folder, "AutoFill written: " + path);
        }

        // ---------- Dual logging ----------
        private static void LogDual(Application app, WorkflowEventArgs args, string fileLogFolder, string message)
        {
            string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message;

            app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Info, line);

            try
            {
                Directory.CreateDirectory(fileLogFolder);
                string path = Path.Combine(fileLogFolder, "log_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch { }

            try
            {
                string existing;
                if (!args.PropertyBag.TryGetValue(PROP_ERROR_MESSAGE, out existing) || existing == null) existing = string.Empty;
                string combined = existing.Length == 0 ? line : (existing + Environment.NewLine + line);
                args.PropertyBag.Set(PROP_ERROR_MESSAGE, combined);
            }
            catch { }
        }

        private static void FailDual(Application app, WorkflowEventArgs args, string message)
        {
            LogDual(app, args, DefaultDiagLogFolder(), "ERROR: " + message);
            args.ScriptResult = false;
        }

        private static string DefaultDiagLogFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LitigationHold", "Logs");
        }

        // ---------- Utils ----------
        private static bool ContainsIgnoreCase(List<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string SanPart(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unnamed";
            char[] bad = Path.GetInvalidFileNameChars();
            char[] arr = s.Trim().ToCharArray();
            for (int i = 0; i < arr.Length; i++)
                for (int b = 0; b < bad.Length; b++)
                    if (arr[i] == bad[b]) { arr[i] = '_'; break; }
            return new string(arr);
        }

        private static string DomainUser(string domain, string sam)
        {
            return string.IsNullOrWhiteSpace(domain) ? sam : (domain + "\\" + sam);
        }

        private static string Q(string s) { return "\"" + s + "\""; }

        private static bool GetBool(PropertyBag pb, string key, bool defVal)
        {
            string v;
            if (!WorkflowPropertyBagHelper.GetString(pb, key, out v)) return defVal;
            v = v.Trim().ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes" || v == "y";
        }

        // ---------- PropertyBag helper ----------
        public static class WorkflowPropertyBagHelper
        {
            public static bool GetString(PropertyBag pb, string propName, out string value)
            {
                value = null;
                try
                {
                    object obj;
                    if (!pb.TryGetValue(propName, out obj)) return false;
                    if (obj == null) return false;
                    string s = obj as string ?? obj.ToString();
                    if (string.IsNullOrWhiteSpace(s)) return false;
                    value = s.Trim();
                    return true;
                }
                catch { return false; }
            }

            public static string Dump(PropertyBag pb)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("PropertyBag diagnostic:");
                string[] known = new string[] {
                    "propFilepath","propFilePath","FormXmlPath","xmlPath","propNoAcl","propErrorMessage"
                };
                for (int i = 0; i < known.Length; i++)
                {
                    try
                    {
                        object obj;
                        bool has = pb.TryGetValue(known[i], out obj);
                        string val = has ? (obj == null ? "<null>" : obj.ToString()) : "<missing>";
                        sb.AppendLine(" - " + known[i] + " = " + val);
                    }
                    catch { sb.AppendLine(" - " + known[i] + " = <error reading>"); }
                }
                return sb.ToString();
            }
        }

        // ---------- Models & XML parser ----------
        private sealed class PersonSpec
        {
            public string PersonFolderName { get; set; }
            public List<string> Contributors { get; set; }
            public PersonSpec() { PersonFolderName = ""; Contributors = new List<string>(); }
        }

        private sealed class Settings
        {
            public string RootFolder { get; set; }
            public string CaseNumber { get; set; }
            public string CaseName { get; set; }
            public string CaseSentBy { get; set; }
            public string CaseDate { get; set; }
            public string Domain { get; set; }
            public string OnBaseSub { get; set; }
            public List<string> Sources { get; set; }
            public List<string> PersonPermissions { get; set; }
            public List<string> SourcePermissions { get; set; }
            public string AutoFillFolder { get; set; }
            public string LogFolder { get; set; }
            public List<PersonSpec> Persons { get; set; }
            public string ServiceUsername { get; set; }
            public string ServicePassword { get; set; }

            public Settings()
            {
                RootFolder = CaseNumber = CaseName = CaseSentBy = CaseDate = Domain = OnBaseSub = "";
                AutoFillFolder = LogFolder = ServiceUsername = ServicePassword = "";
                Sources = new List<string>();
                PersonPermissions = new List<string>();
                SourcePermissions = new List<string>();
                Persons = new List<PersonSpec>();
            }

            public static Settings LoadFromUnityFormXml(string path)
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.PreserveWhitespace = true;
                xdoc.Load(path);

                XmlNamespaceManager ns = new XmlNamespaceManager(xdoc.NameTable);
                ns.AddNamespace("p1", "http://hyland.com/schemas/formdata");
                ns.AddNamespace("d",  "http://hyland.com/schemas/formdata");

                Settings s = new Settings();

                // Domain/RootFolder will be overridden by Configuration Items.
                s.RootFolder = Field(xdoc, ns, "rootFolder");
                s.CaseNumber = Field(xdoc, ns, "caseNumber");
                s.CaseName   = Field(xdoc, ns, "caseName");
                s.CaseSentBy = Field(xdoc, ns, "caseSentBy");
                s.CaseDate   = Field(xdoc, ns, "caseDate");
                s.Domain     = Field(xdoc, ns, "domain");
                if (string.IsNullOrWhiteSpace(s.Domain)) s.Domain = Field(xdoc, ns, "domian");
                s.OnBaseSub  = Field(xdoc, ns, "onbaseSub");
                s.AutoFillFolder = Field(xdoc, ns, "autoFillFolder");
                s.LogFolder      = Field(xdoc, ns, "logFolder");

                s.PersonPermissions = SplitPerms(Field(xdoc, ns, "personPermissions"));
                if (s.PersonPermissions.Count == 0) s.PersonPermissions = SplitPerms(Field(xdoc, ns, "personPermission"));

                s.SourcePermissions = SplitPerms(Field(xdoc, ns, "sourcePermissions"));
                if (s.SourcePermissions.Count == 0) s.SourcePermissions = SplitPerms(Field(xdoc, ns, "sourcePermission"));

                string sourcesCsv = Field(xdoc, ns, "source");
                if (string.IsNullOrWhiteSpace(sourcesCsv)) sourcesCsv = Field(xdoc, ns, "sources");
                List<string> srcs = SplitCsv(sourcesCsv);
                if (srcs.Count > 0) s.Sources = srcs;
                if (s.Sources.Count == 0) s.Sources.Add("Generic");

                XmlNodeList reps = xdoc.SelectNodes("/d:form/p1:repeater", ns);
                if (reps != null)
                {
                    for (int r = 0; r < reps.Count; r++)
                    {
                        XmlNodeList items = reps[r].SelectNodes("p1:repeateritem", ns);
                        if (items == null) continue;

                        for (int it = 0; it < items.Count; it++)
                        {
                            XmlNode item = items[it];

                            PersonSpec p = new PersonSpec();
                            p.PersonFolderName = ItemField(item, ns, "foldername31");

                            string mailFlag = ItemField(item, ns, "mail46");

                            string c1 = ItemField(item, ns, "employeeuserid27");
                            string c2 = ItemField(item, ns, "employeeuseriddatasetonly28");
                            string c3 = ItemField(item, ns, "aliasname25");

                            // Create folder only when there is value in userid (employeeuserid27)
                            if (string.IsNullOrWhiteSpace(c1))
                                continue;

                            // Support comma-separated employee IDs: grant access for every ID in the list
                            List<string> c1Ids = SplitCsv(c1);
                            for (int ci = 0; ci < c1Ids.Count; ci++)
                                AddIf(p.Contributors, c1Ids[ci]);

                            List<string> c2Ids = SplitCsv(c2);
                            for (int ci = 0; ci < c2Ids.Count; ci++)
                                AddIf(p.Contributors, c2Ids[ci]);

                            AddIf(p.Contributors, c3);

                            // Only add employees where MAIL tag is null/blank
                            if (!string.IsNullOrWhiteSpace(p.PersonFolderName) &&
                                string.IsNullOrWhiteSpace(mailFlag))
                            {
                                s.Persons.Add(p);
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(s.CaseNumber)) throw new InvalidOperationException("caseNumber missing in XML.");
                if (s.Persons.Count == 0) throw new InvalidOperationException("No person rows (foldername31) found after filters.");

                return s;
            }

            private static void AddIf(List<string> list, string value)
            {
                value = SafeTrim(value);
                if (!string.IsNullOrWhiteSpace(value) && !ContainsIgnoreCase(list, value)) list.Add(value);
            }

            private static List<string> SplitCsv(string csv)
            {
                List<string> list = new List<string>();
                if (string.IsNullOrWhiteSpace(csv)) return list;
                string[] parts = csv.Split(new char[] { ',', ';', '|', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string v = SafeTrim(parts[i]);
                    if (v.Length > 0 && !ContainsIgnoreCase(list, v)) list.Add(v);
                }
                return list;
            }

            private static List<string> SplitPerms(string s)
            {
                List<string> list = new List<string>();
                if (string.IsNullOrWhiteSpace(s)) return list;

                string[] parts = s.Split(new char[] { ',', ';', '|', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string t = SafeTrim(parts[i]).ToUpperInvariant();
                    if (t.Length == 0) continue;

                    if (t == "RW" || t == "WR") { AddOnce(list, "(OI)(CI)R"); AddOnce(list, "(OI)(CI)W"); continue; }
                    if (t == "X" || t == "E") t = "RX";

                    if (t == "F" || t == "M" || t == "RX" || t == "R" || t == "W")
                    {
                        AddOnce(list, "(OI)(CI)" + t);
                    }
                }
                return list;
            }

            private static string Field(XmlDocument xdoc, XmlNamespaceManager ns, string name)
            {
                XmlNodeList fields = xdoc.SelectNodes("/d:form/p1:field", ns);
                if (fields != null)
                {
                    for (int i = 0; i < fields.Count; i++)
                    {
                        XmlNode f = fields[i];
                        XmlNode n = f.SelectSingleNode("p1:name", ns);
                        if (n != null && string.Equals(SafeTrim(n.InnerText), name, StringComparison.OrdinalIgnoreCase))
                        {
                            XmlNode v = f.SelectSingleNode("p1:value", ns);
                            return v == null ? string.Empty : SafeTrim(v.InnerText);
                        }
                    }
                }
                return string.Empty;
            }

            private static string ItemField(XmlNode item, XmlNamespaceManager ns, string name)
            {
                XmlNodeList fields = item.SelectNodes("p1:field", ns);
                if (fields != null)
                {
                    for (int i = 0; i < fields.Count; i++)
                    {
                        XmlNode f = fields[i];
                        XmlNode n = f.SelectSingleNode("p1:name", ns);
                        if (n != null && string.Equals(SafeTrim(n.InnerText), name, StringComparison.OrdinalIgnoreCase))
                        {
                            XmlNode v = f.SelectSingleNode("p1:value", ns);
                            return v == null ? string.Empty : SafeTrim(v.InnerText);
                        }
                    }
                }
                return string.Empty;
            }

            private static string SafeTrim(string x) { return x == null ? string.Empty : x.Trim(); }

            private static bool ContainsIgnoreCase(List<string> list, string value)
            {
                for (int i = 0; i < list.Count; i++)
                    if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }
    }
}
