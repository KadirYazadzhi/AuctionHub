# 🔨 AuctionHub - Premium Digital Marketplace (ASP.NET Advanced)

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-cyan?style=flat&logo=nuget)](https://docs.microsoft.com/en-us/ef/core/)
[![SignalR](https://img.shields.io/badge/Real--Time-SignalR-orange?style=flat)](https://dotnet.microsoft.com/en-us/apps/aspnet/signalr)
[![Redis](https://img.shields.io/badge/Caching-Redis-red?style=flat&logo=redis)](https://redis.io/)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?style=flat&logo=bootstrap)](https://getbootstrap.com/)
[![Live Demo](https://img.shields.io/badge/Live_Demo-Custom_K8s-blue?style=flat)](https://auctionhub.kadir.bg)

**AuctionHub** is a high-performance, full-stack ASP.NET Core platform designed for real-world auction scenarios. It features a secure financial escrow system, real-time bid updates, a unified messaging center, and advanced administrative tools for moderation and auditing.

<br />
<img src="./preview/home-preview.png" width="100%" alt="AuctionHub Home Page" />
<br />

---

## 🌟 Advanced Features (v2.0 Update)

### ⚡ Real-Time & Performance
* **SignalR Real-Time Bidding:** Prices update instantly across all clients without page refreshes.
* **Global SignalR Notifications:** Receive "Outbid" alerts via beautiful toasts anywhere on the site.
* **Redis Distributed Caching:** Categories and high-traffic metadata are cached for lightning-fast performance.
* **Messenger Hub:** A dedicated real-time private messaging system for buyers, sellers, and administrators.

### 💰 Professional Finance & Escrow
* **Secure Escrow System:** Bidded funds are automatically locked and only released to the seller upon delivery confirmation.
* **Platform Commissions:** Automatic deduction of platform fees (default 5%) upon successful sales.
* **Withdrawal System:** Users can securely withdraw funds from their digital wallet with full transaction logging.
* **Dispute Resolution:** Built-in mechanism for buyers to freeze funds and open disputes for admin review.

### 🛡️ Administrative Power
* **Comprehensive Audit Logs:** Transparent tracking of all administrative actions (suspensions, setting changes, resolutions).
* **Financial Export:** Generate and download complete transaction history in Excel-compatible CSV format.
* **Master Chat Access:** Administrators can enter any conversation to assist in dispute resolution.
* **Advanced User Control:** Capabilities for locking accounts, shadow-banning, and balance adjustments.

---

## 🏗️ Technical Architecture

The solution follows **Clean Architecture** principles with a 4-layer separation:

*   **Domain Layer:** Enterprise entities and core business logic (Auctions, Bids, Transactions).
*   **Application Layer:** Service implementations, DTOs, and interface definitions. Independent of data access technology.
*   **Infrastructure Layer:** EF Core Data Context, Migrations, Email Services (MailKit), and Cloudinary integration.
*   **Web Layer:** ASP.NET Core MVC with Areas (Admin/Identity), SignalR Hubs, and a highly responsive Bootstrap 5 UI.

### Stack Details
| Category | Technology |
|-----------|------------|
| **Core** | .NET 8.0 (C# 12) |
| **Persistence** | MS SQL Server + Entity Framework Core |
| **Real-time** | SignalR (with Redis backplane support) |
| **Caching** | Redis (Distributed Cache) |
| **Images** | Cloudinary API |
| **Mailing** | MailKit (SMTP) + Mailtrap API |
| **Testing** | xUnit, Moq (>75% logic coverage) |

---

## 📂 Project Structure

```text
AuctionHub/
├── AuctionHub/                   # Web Layer (MVC, Hubs, Areas)
│   ├── Areas/Admin               # Administration Logic
│   ├── Areas/Identity            # Auth & Profile Management
│   └── Controllers/Chat          # New Messenger Hub
├── AuctionHub.Application/       # Logic Layer (Services & DTOs)
├── AuctionHub.Domain/            # Enterprise Layer (Entities)
├── AuctionHub.Infrastructure/    # Data & External Services
└── AuctionHub.Tests/             # xUnit Test Suite
```

---

## 🚀 Installation & Setup

1. **Docker Infrastructure:**
   ```bash
   docker-compose up -d
   # Starts MSSQL (Port 5899) and Redis (Port 6379)
   ```

2. **Database Initialization:**
   ```bash
   dotnet tool restore
   dotnet ef database update --project AuctionHub.Infrastructure --startup-project AuctionHub
   ```

3. **Run Application:**
   ```bash
   dotnet run --project AuctionHub
   ```

---

## 🧪 Testing Coverage

The project maintains a rigorous test suite with **over 75% coverage** of core business services.

* **AuctionService:** Bidding logic, extensions (anti-snipe), commissions, and retention policies.
* **WalletService:** Balance integrity, transactions, and withdrawal validations.
* **ChatService:** Real-time messaging security and session management.
* **MessageService:** Contact form processing and admin auto-replies.

Run tests: `dotnet test`

---

## 🛡️ Security Best Practices
* **Anti-CSRF:** Strict use of `[ValidateAntiForgeryToken]` on all state-changing actions.
* **Concurrency:** `RowVersion` implementation to prevent "Double Bidding" or "Double Spending".
* **Asset Retention:** Photos of sold items are preserved for legal/audit purposes even if the listing is archived.
* **Input Sanitization:** Native Razor escaping + server-side validation against XSS and injection.

---

*Developed for the SoftUni ASP.NET Advanced Course - February 2026*
