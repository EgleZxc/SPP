using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestEngine;   

namespace ApplicationUnderTest
{
    public class Account
    {
        public int Id { get; set; }
        public string ServiceName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public override string ToString() => $"{ServiceName} ({UserName})";
    }

    public class PasswordManager
    {
        private readonly List<Account> _accounts = new List<Account>();
        private int _nextId = 1;

        public Account AddAccount(string serviceName, string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Название сервиса не может быть пустым.");
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("Имя пользователя не может быть пустым.");
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Пароль не может быть пустым.");

            if (_accounts.Any(a => a.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase) 
                                 && a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Запись с таким сервисом и именем пользователя уже существует.");

            var account = new Account
            {
                Id = _nextId++,
                ServiceName = serviceName,
                UserName = userName,
                Password = password
            };
            _accounts.Add(account);
            return account;
        }

        public Account GetAccount(int id) => _accounts.FirstOrDefault(a => a.Id == id);

        public List<Account> FindByService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                return new List<Account>();
            return _accounts
                .Where(a => a.ServiceName.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public bool UpdatePassword(int id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("Пароль не может быть пустым.");
            var account = GetAccount(id);
            if (account == null) return false;
            account.Password = newPassword;
            return true;
        }

        public bool DeleteAccount(int id)
        {
            var account = GetAccount(id);
            return account != null && _accounts.Remove(account);
        }

        public List<Account> GetAllAccounts() => _accounts.ToList();

        public static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;
            bool hasDigit = password.Any(char.IsDigit);
            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            return hasDigit && hasUpper && hasLower;
        }

        public async Task<int> GetAccountsCountAsync()
        {
            await Task.Delay(50);
            return _accounts.Count;
        }

        public bool HasAccounts() => _accounts.Any();
    }

    public class SharedPasswordContext : ISharedContext
    {
        public PasswordManager Manager { get; private set; } = new PasswordManager();
        public int InitialCount { get; set; }

        public void Initialize()
        {
            Manager.AddAccount("Google", "user@gmail.com", "Pass1234");
            Manager.AddAccount("Facebook", "john_doe", "Secure456");
            InitialCount = Manager.GetAllAccounts().Count;
            Console.WriteLine("Общий контекст менеджера паролей инициализирован.");
        }

        public void Dispose()
        {
            Console.WriteLine("Общий контекст менеджера паролей освобождён.");
        }
    }
}