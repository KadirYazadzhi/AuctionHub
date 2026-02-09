# 🔨 AuctionHub v2.0

![Build Status](https://img.shields.io/badge/Build-Passing-success?style=for-the-badge&logo=github)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![Status](https://img.shields.io/badge/Status-Completed-success?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

> **AuctionHub** is a premium, feature-rich online auction marketplace built with ASP.NET Core 8.0. It bridges the gap between buyers and sellers with a modern "Glassmorphism" UI, real-time-like interactivity, and a robust financial system.

---

## 🌟 Key Features & Implementation

### 🎨 Modern User Experience (UI/UX)
*   **Glassmorphism Design:** A cohesive visual language using translucent cards (`rounded-5`), soft shadows, and gradients.
*   **Responsive Layout:** Fully optimized for mobile, tablet, and desktop using Bootstrap 5 grid.
*   **Public Profiles:** Clickable seller names lead to a dedicated profile page filtering their active listings.
*   **Informational Pages:** Dedicated pages for *Help Center* (FAQ), *Trust & Safety*, and *Selling Tips* with consistent styling.

### 🛒 Auction Ecosystem
*   **Dynamic Listings:** Create auctions with Title, Description, Start Price, Min Increase, and "Buy It Now" options.
*   **Advanced Filtering:** Filter by **Price Range**, **Category**, **Status** (Active/Closed), and Sorting (Price/Date) on all listing pages.
*   **Image Handling:** Secure file upload with validation (Type/Size) and automatic replacement logic.
*   **Watchlist:** Users can "star" items to track them in a dedicated dashboard.

### 💰 Financial System (Wallet & Transactions)
*   **Digital Wallet:** Every user has a secure wallet for deposits and payments.
*   **Escrow Logic:**
    *   When a bid is placed, funds are **locked** (deducted immediately).
    *   If outbid, funds are **automatically refunded** to the previous bidder.
    *   "Buy It Now" instantly transfers funds and closes the auction.
*   **Transaction History:** A global ledger tracks every move (Deposit, Bid, Refund, Purchase) for full transparency.

### 🛡️ Security & Administration
*   **Admin Panel:** A powerful dashboard (`/Admin`) for system oversight.
    *   **User Management:** Lock/Unlock accounts, adjust balances manually.
    *   **Global Ledger:** View all financial transactions in the system.
    *   **System Announcements:** Send global notifications to all users.
*   **Roles:** Strict separation between `User` and `Administrator`.
*   **Concurrency Control:** Uses database transactions to prevent **Race Conditions** (e.g., two users bidding at the exact same millisecond).

### 🤖 Automation & Notifications
*   **Background Service:** `AuctionCleanupService` runs every minute to:
    *   Close expired auctions automatically.
    *   Notify winners ("You won!") and sellers ("Item sold").
*   **Notification System:** Internal inbox for outbid alerts, sales, and system messages.
*   **Live Badge:** A notification bell in the navbar polls for new messages every 60 seconds.

### ✅ Testing
*   **Unit Tests (`AuctionHub.Tests`):** A dedicated xUnit project covers the core `AuctionService`.
    *   Verifies bidding logic, refund accuracy, self-bidding prevention, and "Buy It Now" execution.
    *   Uses **Moq** and **InMemoryDatabase** for isolated testing.

---

## 🛠️ Technical Stack

| Component | Technology | Description |
| :--- | :--- | :--- |
| **Framework** | ASP.NET Core 8.0 MVC | The backbone of the application. |
| **Database** | SQL Server + EF Core | Relational data storage with Code-First migrations. |
| **Identity** | ASP.NET Core Identity | Secure authentication, registration, and role management. |
| **Background Tasks** | IHostedService | For the automated auction closer. |
| **Testing** | xUnit + Moq | For verifying business logic stability. |
| **Frontend** | Razor + Bootstrap 5 | Server-side rendering with custom CSS variables. |

---

## 🚀 Getting Started

1.  **Clone the repository**
    ```bash
    git clone https://github.com/YourUsername/AuctionHub.git
    ```
2.  **Configure Database**
    Update `appsettings.json` with your connection string.
3.  **Apply Migrations**
    ```bash
    dotnet ef database update
    ```
4.  **Run the App**
    ```bash
    dotnet run
    ```
    *Note: The first run seeds an Admin user (`admin@auctionhub.com` / `Admin123!`) and default categories.*

---

## 🔮 Roadmap (Future Ideas)

Here is a comprehensive list of features planned for future development (e.g., for the ASP.NET Advanced course):

### 1. Real-Time Interactivity (SignalR)
*   **Live Bidding:** Price updates instantly on everyone's screen without refreshing.
*   **Live Chat:** A chat room for each auction or direct messages between buyer and seller.
*   **Push Notifications:** Toast notifications that pop up even if you are on a different tab.

### 2. Advanced Payments
*   **Stripe/PayPal Integration:** Replace the "Mock Wallet" with real payment processing.
*   **Payouts:** Allow sellers to withdraw their wallet balance to a bank account.

### 3. Social & Reputation
*   **Rating System:** Buyers and Sellers rate each other (1-5 stars) after a transaction.
*   **Comments/Q&A:** A public comment section under each auction for asking details about the item.
*   **Social Login:** Login with Google/Facebook.

### 4. Technical Enhancements
*   **Web API:** Expose listing data via REST API for a future mobile app (React Native/Flutter).
*   **Cloud Storage:** Move images to Azure Blob Storage or AWS S3 instead of local `wwwroot`.
*   **Redis Caching:** Cache the "Explore" page to handle thousands of concurrent users.

### 5. Gamification
*   **Badges:** Awards for "Top Seller", "Early Adopter", or "Verified Collector".
*   **Leaderboards:** "Most Active Bidders" or "Highest Sales" lists.

---

## 🤝 Contributing

This project was developed as part of the SoftUni ASP.NET Fundamentals course. Contributions are welcome!

## 📜 License

Distributed under the MIT License.