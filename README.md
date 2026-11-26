# Personal Finance Tracker App
	
The Personal Finance Tracker is a responsive web application, that helps users integrate and visualize their finances, with minimum efforts, by providing easy to use interface and tools such as budgeting and goal settings with  reports and charts.
---	

	
## 🛠️ Technologies:

* HTML
* CSS 
* JavaScript
* Bootstrap v5.3.3
* jQuery v3.7.1
* Plaid
* table-to-excel
* ASP.Net Core 10.0
* C#
* Dapper
* SyncFusion (Charts)
* BCrypt
* Serilog 
* NUnit
* MySQL
* SQL/T-SQL (Statement, Stored Procedure, Function, Agent)
* MySQL Workbench
* Git
* Github
* Azure (App Service with CI/CD and WebJobs)
* Oracle Cloud (MySQL HeatWave, Virtual Cloud Networks, Reversed IP)
* Notepad++	
---


## ✨ Features

* **Security:** Every information of the user is secured with the best aspects of transmission and storage, and can only be accessed after successful log-in.
* **Automation:** Real time data sync from any financial institutions (affiliated with Plaid) after one-time setup.
* **Easier Maintenance:** User friendly GUI to populate and maintain data within application.
* **Cross-Platform Compatibility:** Built on .NET Core, ensuring native performance across Windows, macOS, and Linux environments.
---


## 🚀 Workflow
 
* **User Registration:** Every user is required to register with their details including log-in credentials before app usage.
* **Log-In:** After successful registration, users must log-in using their credentials to use app features.
* **Accounts:** Every transaction within this application must belongs an account. So, to begin work with this application user, must first create an account. Users can also, create linked accounts of their financial institutions by using Plaid intermediary to attempt direct sync at next log-in.
* **Categories:** Categories transforms your transaction data to the information by categorizing it, so that it can be visualized in the dashboard charts. 
* **Tags:** Tags provides another type of filter, so that you can determine/inspect/bifurcate the data in parallel to categories.
* **Budgets:** Allows the user to set the budget per category and per month, which can be compared with actual cashflow in reports. Budget settings can also be configured for automization for next entries.
* **Goals:** Allows the user to set goal for a period, which can be measured with actual finances.
* **Transactions:** Transactions are the information source for this application. Every analysis of this application for the user's finances are being retrieved from transactions. Users can provide the transactions in three ways. Manually, Bulk Upload using CSV File Import (When import automization through Plaid is not available or supplied data are not in correct format), and Automization by Linking to their external financial institutes through Plaid intermediary.
* **Rules:** Rules performs automization of category and/or goal assignment while the bulk CSV data upload and linked transactions sync as well as direct run through Rules GUI. Note: Rules are not assigned to transactions those are created manually.
* **Recurring Schedules:** Another automatic transaction entry at a specific interval until pre-configured duration.
* **Dashboard:** Provides the charts for user's visualization of finances for the selected period.
* **Reports:** Displays downloadable reports of the financial transactions like Income Vs Expenses, Net Worth and Budget comparisions on the monthly or supplied whole period basis.
---


## ⚠️ Limitations:

* **Password Reset:** This is currently not implemented due to un-developed profile module.	
