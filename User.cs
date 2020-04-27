using Mono.Web;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace ReCaptchaV2
{
    public class User
    {
        public enum AccountState
        {
            None = 0,
            ReadedMyProfile = 1,
            AddMainUser = 2,
            AddedMainUser = 3,
            AcceptGroups = 4,
            AcceptedGroups = 5,
            PostToGroups = 6,
            PostedToGroups = 7,
            Banned = 8,
        };

        Form1 mainForm = null;
        string AccountName = "";
        string Password = "";
        public string ProfileName = "";
        public string ProfileUserName = "";
        public string ProfileID = "";

        public Thread processThread = null;
        public AccountState WaitingState = AccountState.None;

        public AccountState AccState = AccountState.None;
        public bool IsConnected = false;

        ChromeDriver _driver = null;
        public Thread backgroundThread = null;
        private Random randomize = new Random();

        public User(Form1 form, string accountName, string password)
        {
            mainForm = form;
            AccountName = accountName;
            Password = password;
        }

        public void Connect()
        {
            if (AccState == AccountState.Banned)
            {
                mainForm.WriteLog(AccountName, "-> Hesap Banlı, Girilmedi.");
                return;
            }

            Thread.Sleep(randomize.Next(1000, 30000));

            ChromeOptions options = new ChromeOptions();
            //options.AddExtensions("fck.crx");
            options.AddArguments("--disable-web-security");
            options.AddArgument("user-agent=Mozilla/5.0 (Android 9; Mobile; rv:68.0) Gecko/68.0 Firefox/68.0");

            try
            {
                _driver = new ChromeDriver(options);
                //_driver.Manage().Window.Minimize();

                if (_driver == null)
                {
                    mainForm.WriteLog(AccountName, "-> Driver is null!! Connect");
                    return;
                }

                _driver.Navigate().GoToUrl("https://mbasic.facebook.com/login.php");
                LoginAccount();
            }
            catch (Exception e)
            {
                mainForm.WriteLog(AccountName, e.Message);
            }
        }

        public void LoginAccount()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "-> Driver is null!! LoginAccount");
                return;
            }

            _driver.SwitchTo().DefaultContent();
            var email = _driver.FindElement(By.Id("m_login_email"));
            var pass = _driver.FindElement(By.Name("pass"));

            email.SendKeys(AccountName);
            Thread.Sleep(1000);
            pass.SendKeys(Password);
            Thread.Sleep(1000);

            var loginBtn = _driver.FindElement(By.Name("login"));
            loginBtn.Click();

            if (IsBanned())
            {
                mainForm.WriteLog(AccountName, "-> HESAP KİLİTLENDİ!!");
                UpdateUserState(AccountState.Banned);

                LogOut();
                return;
            }

            mainForm.WriteLog(AccountName, "Başarıyla Giriş Yapıldı. Durum: " + AccState.ToString());

            ReadProfile();

            IsConnected = true;
        }

        public void ProcessThread()
        {
            if (WaitingState == AccountState.AddMainUser)
                AddMainUser();
            else if (WaitingState == AccountState.AcceptGroups)
                AcceptGroups();
            else if (WaitingState == AccountState.PostToGroups)
                PostToGroups();
        }

        public void DoProcess(AccountState state)
        {
            if (!IsConnected)
                return;

            if (processThread != null)
                processThread.Abort();

            WaitingState = state;

            processThread = new Thread(new ThreadStart(ProcessThread));
            if (processThread == null)
            {
                mainForm.WriteLog(AccountName, "ERROR! Thread Başlatılamadı! DoProcess");
                return;
            }
            processThread.Start();
        }

        public void UpdateUserState(AccountState state)
        {
            if (AccState == AccountState.Banned)
                return;

            AccState = state;
            mainForm.WriteLog(AccountName, "Durum Güncellendi: " + state.ToString());

            SaveProfileInfo();
        }

        bool IsBanned()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "-> Driver is null!! IsBanned");
                return false;
            }

            return _driver.Url.Contains("/checkpoint/");
        }


        void ReadProfile()
        {
            if (ProfileUserName.Length > 0 && ProfileName.Length > 0 && ProfileName.Length > 0)
                return;

            _driver.Navigate().GoToUrl("https://mbasic.facebook.com/profile");
            Thread.Sleep(1000);

            var uri = new Uri(_driver.Url);
            string parse = _driver.Url.Replace("?_rdr#_", string.Empty).Replace("#", string.Empty).Replace("?", string.Empty);
            var parsed = Regex.Split(parse, "/");
            ProfileUserName = parsed[parsed.Length - 1];

            var links = _driver.FindElements(By.TagName("a"));

            foreach (var link in links)
            {
                var href = link.GetAttribute("href");
                if (href.Contains("lst") && href.Contains(ProfileUserName))
                {
                    var queryString = href.Substring(href.IndexOf('?')).Split('#')[0];
                    var lst = HttpUtility.ParseQueryString(queryString)["lst"];

                    ProfileID = lst.Split(':')[0];
                    break;
                }
            }

            var btnLogout = _driver.FindElement(By.Id("mbasic_logout_button"));
            ProfileName = mainForm.Between(btnLogout.Text, "(", ")");

            UpdateUserState(AccountState.ReadedMyProfile);
        }

        public void SaveProfileInfo()
        {
            if (ProfileName.Length > 0)
                mainForm.AccountDetailFile.Write("ProfileName", ProfileName, AccountName);
            if (ProfileUserName.Length > 0)
                mainForm.AccountDetailFile.Write("ProfileUserName", ProfileUserName, AccountName);
            if (ProfileID.Length > 0)
                mainForm.AccountDetailFile.Write("ProfileID", ProfileID, AccountName);

            mainForm.AccountDetailFile.Write("AccState", AccState.ToString(), AccountName);
        }

        void AddMainUser()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "-> Driver is null!! AddMainUser");
                return;
            }

            if (mainForm.mainUser.ProfileName.Length == 0)
            {
                mainForm.WriteLog(AccountName, "-> Ana Hesap Eklenemedi, Ana Hesap Boş!");
                return;
            }

            if (AccState >= AccountState.AddedMainUser)
            {
                mainForm.WriteLog(AccountName, "-> Ana Hesap Zaten Eklenmiş!");
                return;
            }

            mainForm.WriteLog(AccountName, "Ana Hesap Ekleniyor");

            UpdateUserState(AccountState.AddMainUser);

            _driver.Navigate().GoToUrl("https://mbasic.facebook.com/" + mainForm.mainUser.ProfileUserName);
            Thread.Sleep(2000);

            var div = _driver.FindElement(By.Id("objects_container"));
            var links = div.FindElements(By.TagName("a"));

            foreach (var link in links)
            {
                try
                {
                    if (link.GetAttribute("href").Contains("mobile/friends/profile_add_friend.php"))
                    {
                        _driver.Navigate().GoToUrl(link.GetAttribute("href"));
                        break;
                    }
                }
                catch (Exception e)
                {
                    mainForm.WriteLog(AccountName, e.Message);
                }
            }

            UpdateUserState(AccountState.AddedMainUser);
        }

        void AcceptGroups()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "-> Driver is null!! AcceptGroups");
                return;
            }

            if (mainForm.mainUser.ProfileName.Length == 0)
            {
                mainForm.WriteLog(AccountName, "-> Gruplar Kabul Edilemedi, Ana Hesap Boş!");
                return;
            }

            if (AccState >= AccountState.AcceptedGroups)
            {
                mainForm.WriteLog(AccountName, "-> Gruplar Zaten Kabul Edilmiş");
                return;
            }

            mainForm.WriteLog(AccountName, "Gruplar Kabul Ediliyor");

            UpdateUserState(AccountState.AcceptGroups);

            var confirmLinks = new List<string>();

            _driver.Navigate().GoToUrl("https://mbasic.facebook.com/notifications.php");

            var tables = _driver.FindElements(By.TagName("table"));
            if (tables.Count == 0)
            {
                mainForm.WriteLog(AccountName, "-> Gruplar Kabul Edilemedi, Table Count Boş!");
                return;
            }

            foreach (var table in tables)
            {
                var notifications = table.FindElements(By.TagName("a"));
                foreach (var notification in notifications)
                {
                    var spans = notification.FindElements(By.ClassName("blueName"));
                    if (spans.Count == 0)
                        continue;

                    var link = notification.GetAttribute("href");

                    foreach (var span in spans)
                    {
                        if (span.Text.ToUpper().Contains(mainForm.mainUser.ProfileName.ToUpper()) && link.Contains("group_id"))
                            confirmLinks.Add(link);
                    }
                }
            }

            foreach (var cLink in confirmLinks)
            {
                _driver.Navigate().GoToUrl(cLink);
                var form = _driver.FindElement(By.Id("objects_container")).FindElements(By.TagName("form"))[0];
                var joinBtn = form.FindElements(By.TagName("input"))[2];

                Thread.Sleep(2000);
                if (!joinBtn.Displayed)
                    continue;

                joinBtn.Click();
                Thread.Sleep(randomize.Next(1000, 5000));
            }

            UpdateUserState(AccountState.AcceptedGroups);
        }

        void PostToGroups()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "-> Driver is null!! PostToGroups");
                return;
            }

            if (mainForm.mainUser.ProfileName.Length == 0)
            {
                mainForm.WriteLog(AccountName, "-> Gruplara Post Atılamadı, Ana Hesap Boş!");
                return;
            }

            if (AccState >= AccountState.PostedToGroups)
            {
                mainForm.WriteLog(AccountName, "-> Gruplara Zaten Post Atılmış");
                return;
            }

            mainForm.WriteLog(AccountName, "Gruplara Post Atılıyor");

            UpdateUserState(AccountState.PostToGroups);













            UpdateUserState(AccountState.PostedToGroups);
        }

        public void LogOut()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "-> Driver is null!! LogOut");
                return;
            }
            
            mainForm.WriteLog(AccountName, "Çıkış Yapıldı");

            IsConnected = false;
            _driver.Quit();
        }
    }
}
