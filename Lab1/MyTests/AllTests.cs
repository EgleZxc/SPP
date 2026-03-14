using System;
using System.Linq;
using System.Threading.Tasks;
using ApplicationUnderTest;
using TestEngine;

namespace MyTests
{
    [TestSuite]
    public class PasswordManagerTests
    {
        private PasswordManager _manager;

        [Initialize]
        public void Setup()
        {
            _manager = new PasswordManager();
        }

        [Fact(Description = "Добавление новой записи", Priority = 1)]
        public void AddAccount_ValidData_ReturnsAccountWithCorrectProperties()
        {
            var account = _manager.AddAccount("GitHub", "octocat", "P@ssw0rd");
            Assert.Equal("GitHub", account.ServiceName);
            Assert.Equal("octocat", account.UserName);
            Assert.Equal("P@ssw0rd", account.Password);
            Assert.GreaterThan(account.Id, 0);
        }

        [Fact]
        public void AddAccount_EmptyServiceName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _manager.AddAccount("", "user", "pass"));
        }

        [Fact]
        public void AddAccount_EmptyUserName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _manager.AddAccount("Service", "", "pass"));
        }

        [Fact]
        public void AddAccount_EmptyPassword_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _manager.AddAccount("Service", "user", ""));
        }

        [Fact]
        public void AddAccount_DuplicateServiceAndUser_ThrowsInvalidOperationException()
        {
            _manager.AddAccount("Google", "my@gmail.com", "pass123");
            Assert.Throws<InvalidOperationException>(() => _manager.AddAccount("Google", "my@gmail.com", "another"));
        }

        [Fact]
        public void GetAccount_ExistingId_ReturnsCorrectAccount()
        {
            var added = _manager.AddAccount("Twitter", "elon", "tweet");
            var found = _manager.GetAccount(added.Id);
            Assert.NotNull(found);
            Assert.Equal(added.ServiceName, found.ServiceName);
        }

        [Fact]
        public void GetAccount_NonExistingId_ReturnsNull()
        {
            var found = _manager.GetAccount(999);
            Assert.Null(found);
        }

        [Fact]
        public void FindByService_ExistingService_ReturnsMatchingAccounts()
        {
            _manager.AddAccount("GitHub", "dev1", "pass1");
            _manager.AddAccount("GitHub", "dev2", "pass2");
            _manager.AddAccount("Twitter", "user", "pass3");
            var result = _manager.FindByService("GitHub");

            Assert.Equal(2, result.Count);
            foreach (var a in result)
            {
                Assert.Contains("GitHub", a.ServiceName);
            }
        }

        [Fact]
        public void FindByService_PartialMatch_ReturnsAccounts()
        {
            _manager.AddAccount("MyGoogle", "user1", "pass1");
            _manager.AddAccount("GoogleDrive", "user2", "pass2");
            var result = _manager.FindByService("Google");
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void FindByService_EmptyString_ReturnsEmptyList()
        {
            _manager.AddAccount("Service", "user", "pass");
            var result = _manager.FindByService("");
            Assert.Empty(result);
        }

        [Fact]
        public void UpdatePassword_ExistingId_ChangesPasswordAndReturnsTrue()
        {
            var account = _manager.AddAccount("Netflix", "user", "oldPass");
            bool updated = _manager.UpdatePassword(account.Id, "newStrongPass123");
            Assert.True(updated);
            Assert.Equal("newStrongPass123", account.Password);
        }

        [Fact]
        public void UpdatePassword_NonExistingId_ReturnsFalse()
        {
            bool updated = _manager.UpdatePassword(999, "newPass");
            Assert.False(updated);
        }

        [Fact]
        public void UpdatePassword_EmptyNewPassword_ThrowsArgumentException()
        {
            var account = _manager.AddAccount("Service", "user", "pass");
            Assert.Throws<ArgumentException>(() => _manager.UpdatePassword(account.Id, ""));
        }

        [Fact]
        public void DeleteAccount_ExistingId_RemovesAccountAndReturnsTrue()
        {
            var account = _manager.AddAccount("ToDelete", "user", "pass");
            int id = account.Id;
            bool deleted = _manager.DeleteAccount(id);
            Assert.True(deleted);
            Assert.Null(_manager.GetAccount(id));
        }

        [Fact]
        public void DeleteAccount_NonExistingId_ReturnsFalse()
        {
            bool deleted = _manager.DeleteAccount(999);
            Assert.False(deleted);
        }

        [Fact]
        public void GetAllAccounts_ReturnsAllAddedAccounts()
        {
            _manager.AddAccount("A", "u1", "p1");
            _manager.AddAccount("B", "u2", "p2");
            _manager.AddAccount("C", "u3", "p3");
            var all = _manager.GetAllAccounts();
            Assert.Equal(3, all.Count);
            Assert.NotEmpty(all);
        }

        [Fact]
        public void IsStrongPassword_ValidPassword_ReturnsTrue()
        {
            bool result = PasswordManager.IsStrongPassword("StrongP@ss1");
            Assert.True(result);
        }

        [Fact]
        public void IsStrongPassword_TooShort_ReturnsFalse()
        {
            bool result = PasswordManager.IsStrongPassword("Sh0rt");
            Assert.False(result);
        }

        [Fact]
        public void IsStrongPassword_NoDigit_ReturnsFalse()
        {
            bool result = PasswordManager.IsStrongPassword("NoDigitsHere!");
            Assert.False(result);
        }

        [Fact]
        public void IsStrongPassword_NoUpperCase_ReturnsFalse()
        {
            bool result = PasswordManager.IsStrongPassword("nouppercase1");
            Assert.False(result);
        }

        [Fact]
        public async Task GetAccountsCountAsync_ReturnsCorrectCount()
        {
            _manager.AddAccount("Test1", "u1", "p1");
            _manager.AddAccount("Test2", "u2", "p2");
            int count = await _manager.GetAccountsCountAsync();
            Assert.Equal(2, count);
        }

        [Fact]
        public void HasAccounts_WhenAccountsExist_ReturnsTrue()
        {
            _manager.AddAccount("Service", "user", "pass");
            Assert.True(_manager.HasAccounts());
        }

        [Fact]
        public void HasAccounts_WhenNoAccounts_ReturnsFalse()
        {
            Assert.False(_manager.HasAccounts());
        }

        [Fact]
        [TestData("Google", "alice@gmail.com", "pass123")]
        [TestData("Facebook", "bob", "secret456")]
        [TestData("Twitter", "charlie", "tweet789")]
        public void ParameterizedAddTest(string service, string user, string pass)
        {
            var account = _manager.AddAccount(service, user, pass);
            Assert.Equal(service, account.ServiceName);
            Assert.Equal(user, account.UserName);
            Assert.Equal(pass, account.Password);
        }

        [Fact]
        [Skip(Reason = "Тест временно отключён")]
        public void SkippedTest()
        {
            Assert.True(false);
        }

        [Cleanup]
        public void TearDown() { }
    }

    [TestSuite]
    [SharedContextType(typeof(SharedPasswordContext))]
    public class PasswordManagerSharedContextTests
    {
        private readonly PasswordManager _manager;
        private readonly SharedPasswordContext _context;

        public PasswordManagerSharedContextTests(SharedPasswordContext context)
        {
            _context = context;
            _manager = context.Manager;
        }

        [Fact]
        public void SharedContext_InitialCount_IsCorrect()
        {
            Assert.Equal(2, _context.InitialCount);
        }

        [Fact]
        public void SharedContext_Manager_ContainsPreloadedAccounts()
        {
            var all = _manager.GetAllAccounts();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void ModifyContext_AddAccount()
        {
            _manager.AddAccount("NewService", "newuser", "newpass");
            _context.InitialCount = _manager.GetAllAccounts().Count;
        }

        [Fact]
        public void CheckModifiedContext()
        {
            Assert.Equal(3, _context.InitialCount);
        }
    }
}