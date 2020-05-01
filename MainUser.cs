using Mono.Web;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using static ReCaptchaV2.User;

namespace ReCaptchaV2
{
    public class MainUser
    {
        public enum MainAccountState
        {
            None = 10,
            ReadedMyProfile = 11,
            AcceptFriends = 12,
            AcceptedFriends = 13,
            AddToGroups = 14,
            AddedToGroups = 15,
            Banned = 16
        };

        Form1 mainForm = null;
        public string ProfileName = "";
        public string ProfileUserName = "";
        public string ProfileID = "";

        public string AccountName = "";
        string Password = "";
        public MainAccountState MainAccState = MainAccountState.None;
        public bool IsConnected = false;

        ChromeDriver _driver = null;
        public Thread backgroundThread = null;
        private Random randomize = new Random();

        public List<string> GroupList = new List<string>();

        public MainUser(Form1 form, string accountName, string password)
        {
            mainForm = form;
            AccountName = accountName;
            Password = password;
        }

        public void Connect()
        {
            ChromeOptions options = new ChromeOptions();
            //options.AddExtensions("fck.crx");
            options.AddArguments("--disable-web-security");
            options.AddArgument("user-agent=Mozilla/5.0 (Android 9; Mobile; rv:68.0) Gecko/68.0 Firefox/68.0");
            //options.AddArgument("headless");
            _driver = new ChromeDriver(options);
            //_driver.Manage().Window.Minimize();

            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Driver is null!! Connect");
                return;
            }

            _driver.Navigate().GoToUrl("https://mbasic.facebook.com/login.php");
            LoginAccount();
        }


        public void SaveProfileInfo()
        {
            if (ProfileName.Length > 0)
                mainForm.MainAccountDetailFile.Write("ProfileName", ProfileName, AccountName);
            if (ProfileUserName.Length > 0)
                mainForm.MainAccountDetailFile.Write("ProfileUserName", ProfileUserName, AccountName);
            if (ProfileID.Length > 0)
                mainForm.MainAccountDetailFile.Write("ProfileID", ProfileID, AccountName);

            mainForm.MainAccountDetailFile.Write("MainAccState", MainAccState.ToString(), AccountName);
        }

        public void LoginAccount()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Driver is null!! LoginAccount");
                return;
            }

            if (MainAccState == MainAccountState.Banned)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Banlı LoginAccount");
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
                mainForm.WriteLog(AccountName, "Ana Hesap -> KİLİTLENDİ!!");
                UpdateUserState(MainAccountState.Banned);
                LogOut();
                return;
            }

            mainForm.WriteLog(AccountName, "Ana Hesap Başarıyla Giriş Yapıldı. Durum: " + MainAccState.ToString());

            ReadProfile();
            ReadGroups();

            IsConnected = true;
        }

        void ReadProfile()
        {
            if (ProfileUserName.Length > 0 && ProfileName.Length > 0 && ProfileID.Length > 0)
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

            SaveProfileInfo();
        }

        private bool ReadGroups()
        {
            _driver.Navigate().GoToUrl("https://mbasic.facebook.com/groups/?seemore");

            var groupDiv = _driver.FindElement(By.TagName("ul"));
            var tables = groupDiv.FindElements(By.TagName("table"));
            if (tables.Count == 0)
            {
                MessageBox.Show("Ana Hesapta Girilmiş Grup Yok! ", "HATA!");
                return false;
            }

            foreach (var table in tables)
            {
                var link = table.FindElements(By.TagName("a"))[0].GetAttribute("href");
                var uri = new Uri(link);
                string parse = link.Replace(uri.Query, "");
                var parsed = Regex.Split(parse, "/");
                var id = parsed[parsed.Length - 1];

                GroupList.Add(id);
            }

            return true;
        }

        public void UpdateUserState(MainAccountState state)
        {
            if (MainAccState == MainAccountState.Banned)
                return;

            MainAccState = state;
            mainForm.WriteLog(AccountName, "Ana Hesap Durum Güncellendi: " + state.ToString());

            SaveProfileInfo();
        }

        public void DoProcess(MainAccountState state)
        {
            if (!IsConnected || IsBanned(false))
                return;

            if (state == MainAccountState.AcceptFriends)
                AcceptFriends();
            else if (state == MainAccountState.AddToGroups)
                AddToGroups();
        }

        bool IsBanned(bool writeLog = true)
        {
            if (_driver == null)
            {
                if (writeLog)
                    mainForm.WriteLog(AccountName, "Ana Hesap -> Driver is null!! IsBanned");
                return false;
            }

            return _driver.Url.Contains("/checkpoint/");
        }

        void AcceptFriends()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Driver is null!! AcceptFriends");
                return;
            }

            if (MainAccState == MainAccountState.Banned)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Banlı AcceptFriends");
                return;
            }

            if (mainForm.mainUser.ProfileName.Length == 0)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Arkadaşlar Kabul Edilemedi, Ana Hesap Boş!");
                return;
            }

            mainForm.WriteLog(AccountName, "Ana Hesap Arkadaşlar Kabul Ediliyor");

            UpdateUserState(MainAccountState.AcceptFriends);

            var AcceptAddList = new List<string>();
            foreach (var acc in mainForm.AccChromeMap)
            {
                if (acc.Value.ProfileID.Length == 0)
                    continue;

                AcceptAddList.Add(acc.Value.ProfileID);
            }

            var confirmLinks = new List<string>();

            try
            {
                for (int i = 0; i < 100; i++)
                {
                    _driver.Navigate().GoToUrl("https://mbasic.facebook.com/friends/center/requests/?ppk=" + i + "&tid=u_0_0&bph=" + i + "#friends_center_main");

                    var friendDiv = _driver.FindElement(By.Id("friends_center_main"));

                    var links = friendDiv.FindElements(By.TagName("a"));
                    if (links.Count == 0)
                        break;

                    var tables = friendDiv.FindElements(By.TagName("table"));
                    if (tables.Count == 0)
                        break;

                    foreach (var link in links)
                    {
                        var href = link.GetAttribute("href");
                        if (href.Contains("confirm"))
                        {
                            foreach (var ll in AcceptAddList)
                                if (href.Contains(ll))
                                    confirmLinks.Add(href);
                        }
                    }
                    Thread.Sleep(2000);
                }

                foreach (var cLink in confirmLinks)
                {
                    _driver.Navigate().GoToUrl(cLink);
                    Thread.Sleep(5000);
                }
            }
            catch (Exception e)
            {
                mainForm.WriteLog(AccountName, e.Message);
            }

            UpdateUserState(MainAccountState.AcceptedFriends);
        }

        void AddToGroups()
        {
            if (_driver == null)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Driver is null!! AddToGroups");
                return;
            }

            if (MainAccState == MainAccountState.Banned)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Banlı AddToGroups");
                return;
            }

            if (mainForm.mainUser.ProfileName.Length == 0)
            {
                mainForm.WriteLog(AccountName, "Ana Hesap -> Gruplara Eklenemedi, Ana Hesap Boş!");
                return;
            }

            mainForm.WriteLog(AccountName, "Ana Hesap Gruplara Ekleniyor");

            UpdateUserState(MainAccountState.AddToGroups);

            int counter = 0;
            foreach (var group in GroupList)
            {
                try
                {
                    foreach (var user in mainForm.AccChromeMap)
                    {
                        _driver.Navigate().GoToUrl("https://mbasic.facebook.com/groups/members/search/?group_id=" + group);

                        var div = _driver.FindElement(By.Id("objects_container"));
                        var searchBox = div.FindElement(By.Name("query_term"));
                        searchBox.SendKeys(user.Value.ProfileName);

                        var searchBtns = div.FindElements(By.CssSelector("input[type='submit']"));
                        searchBtns[0].Click();

                        _driver.SwitchTo().DefaultContent();

                        var div2 = _driver.FindElement(By.Id("objects_container"));
                        var checks = div2.FindElements(By.CssSelector("input[type='checkbox']"));
                        if (checks.Count == 0)
                            continue;

                        checks[0].Click();

                        var inviteBtns = div2.FindElements(By.CssSelector("input[type='submit']"));
                        inviteBtns[1].Click();

                        if(counter++ % 15 == 0)
                            Thread.Sleep(60000);
                        else
                            Thread.Sleep(10000);
                    }
                }
                catch (Exception e)
                {
                    mainForm.WriteLog(AccountName, e.Message);
                }
            }

            UpdateUserState(MainAccountState.AddedToGroups);
        }

        public void LogOut()
        {
            if (_driver == null)
                return;

            mainForm.WriteLog(AccountName, "Ana Hesap Çıkış Yapıldı");

            IsConnected = false;
            _driver.Quit();
        }
    }
}
