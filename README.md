# AuctionHub - Digital Auction Platform

AuctionHub is a robust full-stack web application built with **ASP.NET Core 8.0 MVC**. The platform facilitates secure, real-time-like auctioning of unique items, featuring a sophisticated financial escrow system, comprehensive administration tools, and a modern, responsive user interface.

---

## 🛠 Tech Stack

- **Backend:** C# 12, ASP.NET Core 8.0 MVC
- **Database:** SQL Server, Entity Framework Core (Code-First)
- **Identity:** ASP.NET Core Identity (Customized)
- **Background Tasks:** IHostedService (Worker)
- **Testing:** xUnit, Moq, InMemoryDatabase
- **Frontend:** Razor Views, Bootstrap 5, Custom CSS3 (Glassmorphism)

---

## 🚀 Key Functionalities

### 1. Auction Management
- **Lifecycle:** Create, Edit, and Manage auctions with dynamic status tracking (Active, Closed, Suspended).
- **Bidding System:** Real-time validation, minimum increment logic, and "Buy It Now" immediate purchase.
- **Soft Delete:** Administrative suspension of listings with automatic participant notification.

### 2. Financial & Escrow System
- **Digital Wallet:** Integrated user balance management with deposit functionality.
- **Automatic Refunds:** Real-time refunding of outbid amounts using a secure escrow logic.
- **Transaction Ledger:** Immutable logging of all financial movements (Bids, Purchases, Deposits, Refunds).

### 3. User Experience & Profiles
- **Public Profiles:** Public-facing seller pages showcasing active listings and ratings.
- **Identity & Security:** Custom registration (Username/Names), role-based access, and avatar management.
- **Watchlist:** Personal dashboard for tracking observed items.

### 4. Administration Panel
- **Oversight:** Centralized dashboard for system statistics.
- **User Management:** Global user list with balance adjustment and account locking capabilities.
- **Audit Logs:** Full visibility into global transactions and contact inquiries.
- **Announcements:** System-wide notification broadcasting.

### 5. Automated Services & Notifications
- **Cleanup Service:** Background worker for automatic auction closure and winner determination.
- **Notification Inbox:** Internal system for bid alerts, sale confirmations, and outbid warnings.

---

## 📸 Visual Overview

### Home Page
![Home Page Placeholder](./wwwroot/images/readme/home.png)

### Auction Discovery
![Explore Auctions Placeholder](./wwwroot/images/readme/explore.png)

### Bidding Interface
![Auction Details Placeholder](./wwwroot/images/readme/details.png)

### Administration Dashboard
![Admin Dashboard Placeholder](./wwwroot/images/readme/admin.png)

---

## 🏗 Architectural Highlights

- **Service Layer Pattern:** Business logic is decoupled from controllers for high testability.
- **Optimistic Concurrency:** Implementation of `RowVersion` [Timestamp] to prevent race conditions during bidding and balance updates.
- **Custom Identity UI:** Fully rewritten Identity pages to match the platform's visual identity.
- **Asynchronous Processing:** End-to-end async/await implementation for optimal performance.

---

## 🧪 Testing

The project includes a dedicated test suite (`AuctionHub.Tests`) covering:
- **Unit Tests:** Critical business logic in `AuctionService`.
- **Edge Cases:** Testing insufficient funds, self-bidding prevention, and refund accuracy.

---

## 🔧 Installation & Setup

1. **Clone & Restore:**
   ```bash
   git clone https://github.com/YourUsername/AuctionHub.git
   dotnet restore
   ```
2. **Database Migration:**
   ```bash
   dotnet ef database update
   ```
3. **Execution:**
   ```bash
   dotnet run
   ```
   *Admin credentials: `admin@auctionhub.com` / `Admin123!`*

---

## 🔮 Roadmap

- **SignalR Integration:** Live bidding updates without page refresh.
- **Payment Gateway:** Integration with Stripe/PayPal for real transactions.
- **Rating System:** Peer-to-peer reviews for buyers and sellers.
- **Web API:** RESTful endpoints for mobile compatibility.

---
**Developed by Kadir Yazadzhi** - *ASP.NET Fundamentals Project*
