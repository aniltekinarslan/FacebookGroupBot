using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using static ReCaptchaV2.MainUser;
using static ReCaptchaV2.User;

namespace ReCaptchaV2
{
    public partial class Form1 : Form
    {
        private readonly object m_lockAccChromeMap = new object();

        public Dictionary<string, User> AccChromeMap = new Dictionary<string, User>();

        public bool ProcessRunning = false;
        public Thread processThread = null;

        public enum ProcessState
        {
            Start = 1,
            MainAccountStartLogin = 2,
            MainAccountLoginedIn = 3,
            AllUsersStartLogin = 4,
            AllUsersLoginedIn = 5,
            AllUsersAddMainUser = 6,
            AllUsersAddedMainUser = 7,
            MainAccountAcceptUsers = 8,
            MainAccountAcceptedUsers = 9,
            MainAccountAddToGroups = 10,
            MainAccountAddedToGroups = 11,
            AllUsersAcceptGroups = 12,
            AllUsersAcceptedGroups = 13,
        };

        ProcessState ProState = ProcessState.Start;

        int logCounter = 0;
        public MainUser mainUser = null;

        public IniFile AccountDetailFile = new IniFile("__HesapDetaylar");
        public IniFile MainAccountDetailFile = new IniFile("__AnaHesapDetaylar");

        public Form1()
        {
            InitializeComponent();

            if (!ReadMainAccount()
            || !ReadAccounts()
            || !ReadMainAccountDetails()
            || !ReadAccountDetails())
                return;
        }


        private bool ReadMainAccount()
        {
            var path = Environment.CurrentDirectory + "\\_AnaHesap.txt";
            if (!File.Exists(path))
            {
                MessageBox.Show("_AnaHesap.txt dosyası bulunamadı!", "HATA!");
                return false;
            }

            var data = File.ReadAllText(path);
            var strLines = Regex.Split(data, "\r\n");

            foreach (var l in strLines)
            {
                if (l == "")
                    continue;

                var line = l.Replace(" ", "").Split(new string[] { "[]" }, StringSplitOptions.None);

                mainUser = new MainUser(this, line[0], line[1]);
                break;
            }

            return true;
        }

        private bool ReadAccounts()
        {
            var path = Environment.CurrentDirectory + "\\_Hesaplar.txt";
            if (!File.Exists(path))
            {
                MessageBox.Show("_Hesaplar.txt dosyası bulunamadı!", "HATA!");
                return false;
            }

            var data = File.ReadAllText(path);
            var strLines = Regex.Split(data, "\r\n");

            lock (m_lockAccChromeMap)
            {
                foreach (var l in strLines)
                {
                    if (l == "")
                        continue;

                    var line = l.Replace(" ", "").Split(new string[] { "[]" }, StringSplitOptions.None);
                    AccChromeMap.Add(line[0], new User(this, line[0], line[1]));

                    var row = new String[2];
                    row[0] = line[0];
                    row[1] = line[1];
                    dg_Accounts.Rows.Add(row[0], row[1]);
                }

                if (dg_Accounts.Rows.Count == 0)
                {
                    MessageBox.Show("_Hesaplar.txt içi boş!! Düzeltin!", "HATA!");
                    return false;
                }
            }

            return true;
        }

        public string Between(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
        }

        private bool ReadAccountDetails()
        {
            if (!File.Exists(AccountDetailFile.GetPath()))
                return true;

            foreach (var acc in AccChromeMap)
            {
                AccChromeMap[acc.Key].ProfileName = AccountDetailFile.Read("ProfileName", acc.Key);
                AccChromeMap[acc.Key].ProfileUserName = AccountDetailFile.Read("ProfileUserName", acc.Key);
                AccChromeMap[acc.Key].ProfileID = AccountDetailFile.Read("ProfileID", acc.Key);

                AccountState state;
                Enum.TryParse(AccountDetailFile.Read("AccState", acc.Key), out state);

                AccChromeMap[acc.Key].AccState = state;
            }

            return true;
        }

        private bool ReadMainAccountDetails()
        {
            if (!File.Exists(MainAccountDetailFile.GetPath()))
                return true;

            mainUser.ProfileName = MainAccountDetailFile.Read("ProfileName", mainUser.AccountName);
            mainUser.ProfileUserName = MainAccountDetailFile.Read("ProfileUserName", mainUser.AccountName);
            mainUser.ProfileID = MainAccountDetailFile.Read("ProfileID", mainUser.AccountName);

            MainAccountState state;
            Enum.TryParse(MainAccountDetailFile.Read("MainAccState", mainUser.AccountName), out state);

            mainUser.MainAccState = state;

            lbl_MainAccount.Text = mainUser.AccountName + " - " + mainUser.ProfileName;
            return true;
        }

        public void WriteLog(string accName, string text)
        {
            if (text.Length < 1)
                return;

            var row = new String[4];

            row[0] = (++logCounter).ToString();
            row[1] = DateTime.Now.ToString();
            row[2] = accName;
            row[3] = text;

            dg_Logs.BeginInvoke(new Action(delegate () { dg_Logs.Rows.Add(Convert.ToInt32(row[0]), row[1], row[2], row[3]); }));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (mainUser.MainAccState >= MainAccountState.ReadedMyProfile)
                mainUser.UpdateUserState(MainAccountState.ReadedMyProfile);

            foreach (var acc in AccChromeMap)
            {
                if (acc.Value.AccState >= AccountState.ReadedMyProfile)
                    acc.Value.UpdateUserState(AccountState.ReadedMyProfile);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ProcessRunning = true;
            processThread = new Thread(new ThreadStart(Process));
            if (processThread == null)
            {
                WriteLog("ARTYGUARD", "ERROR! Thread Başlatılamadı!");
                return;
            }
            processThread.Start();


            WriteLog("ARTYGUARD", "İşlemler Başlatıldı!");
        }

        public void Process()
        {
            while (ProcessRunning)
            {
                Thread.Sleep(1000);

                if (ProState == ProcessState.Start)
                {
                    mainUser.backgroundThread = new Thread(new ThreadStart(mainUser.Connect));
                    if (mainUser.backgroundThread == null)
                        return;
                    mainUser.backgroundThread.Start();

                    ProState = ProcessState.MainAccountStartLogin;
                }

                else if (ProState == ProcessState.MainAccountStartLogin)
                {
                    if (mainUser.IsConnected)
                        ProState = ProcessState.MainAccountLoginedIn;
                }


                else if (ProState == ProcessState.MainAccountLoginedIn)
                {
                    foreach (var acc in AccChromeMap)
                    {
                        acc.Value.backgroundThread = new Thread(new ThreadStart(acc.Value.Connect));
                        if (acc.Value.backgroundThread == null)
                            continue;

                        acc.Value.backgroundThread.Start();
                    }

                    ProState = ProcessState.AllUsersStartLogin;
                }

                else if (ProState == ProcessState.AllUsersStartLogin)
                {
                    int LoginedUserCount = 0;
                    foreach (var acc in AccChromeMap)
                    {
                        if (acc.Value.IsConnected || acc.Value.AccState == AccountState.Banned)
                            LoginedUserCount++;
                    }

                    if (LoginedUserCount == AccChromeMap.Count)
                        ProState = ProcessState.AllUsersLoginedIn;
                }

                else if (ProState == ProcessState.AllUsersLoginedIn)
                {
                    foreach (var acc in AccChromeMap)
                    {
                        if (acc.Value.IsConnected)
                            acc.Value.DoProcess(AccountState.AddMainUser);
                    }

                    ProState = ProcessState.AllUsersAddMainUser;
                }


                else if (ProState == ProcessState.AllUsersAddMainUser)
                {
                    int AddedMainUserCount = 0;
                    foreach (var acc in AccChromeMap)
                    {
                        if (acc.Value.AccState == AccountState.AddedMainUser || acc.Value.AccState == AccountState.Banned)
                            AddedMainUserCount++;
                    }

                    if (AddedMainUserCount == AccChromeMap.Count)
                        ProState = ProcessState.AllUsersAddedMainUser;
                }

                else if (ProState == ProcessState.AllUsersAddedMainUser)
                {
                    mainUser.DoProcess(MainAccountState.AcceptFriends);
                    ProState = ProcessState.MainAccountAcceptUsers;
                }

                else if (ProState == ProcessState.MainAccountAcceptUsers)
                {
                    if (mainUser.MainAccState == MainAccountState.AcceptedFriends)
                        ProState = ProcessState.MainAccountAcceptedUsers;
                }


                else if (ProState == ProcessState.MainAccountAcceptedUsers)
                {
                    mainUser.DoProcess(MainAccountState.AddToGroups);
                    ProState = ProcessState.MainAccountAddToGroups;
                }

                else if (ProState == ProcessState.MainAccountAddToGroups)
                {
                    if (mainUser.MainAccState == MainAccountState.AddedToGroups)
                        ProState = ProcessState.MainAccountAddedToGroups;
                }

                else if (ProState == ProcessState.MainAccountAddedToGroups)
                {
                    foreach (var acc in AccChromeMap)
                    {
                        if (acc.Value.IsConnected)
                            acc.Value.DoProcess(AccountState.AcceptGroups);
                    }

                    ProState = ProcessState.AllUsersAcceptGroups;
                }

                else if (ProState == ProcessState.AllUsersAcceptGroups)
                {
                    int AcceptedGroupsUserCount = 0;
                    foreach (var acc in AccChromeMap)
                    {
                        if (acc.Value.AccState == AccountState.AcceptedGroups || acc.Value.AccState == AccountState.Banned)
                            AcceptedGroupsUserCount++;
                    }

                    if (AcceptedGroupsUserCount == AccChromeMap.Count)
                        ProState = ProcessState.AllUsersAcceptedGroups;
                }

                else if (ProState == ProcessState.AllUsersAcceptedGroups)
                {
                    MessageBox.Show("Tüm İşlemler Tamamlandı!");
                    ProcessRunning = false;
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            mainUser.LogOut();

            foreach (var acc in AccChromeMap)
                acc.Value.LogOut();

            ProcessRunning = false;
            if (processThread != null)
                processThread.Abort();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void tmr_Tick(object sender, EventArgs e)
        {
            lbl_Process.Text = ProState.ToString();
        }
    }
}
