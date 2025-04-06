# Project Documentation

> [!IMPORTANT] 
> In this section, I will focus solely on the main project `CurrencyRateFetcher`. Information about the additional project `CurrencyApi` is outlined at the end of the main part in the section [Additional Technology](#Additional-Technology)

## General Description
### Objective
Develop a C# application that receives and stores exchange rates (EUR) on a daily basis.

### Description
The project was developed in **Visual Studio 2022**, utilizing the built-in **Publish** function to simplify the deployment process. The database is hosted on a Linux virtual machine, with a connection established via HeidiSQL over TCP/IP. After successful deployment, the application was integrated with CRON, which was configured directly on the virtual machine to execute the task daily at 00:00 (except Sundays and Mondays) and conveniently retrieve the previous day's exchange rates.

> [!IMPORTANT] 
> This program runs at 00:00 and retrieves data for the previous day. This is because the [European Central Bank](https://www.ecb.europa.eu/stats/policy_and_exchange_rates/euro_reference_exchange_rates/html/index.en.html) publishes data daily, except on weekends and holidays, around at 16:00 CET. Therefore there is no need to retrieve the data on Sundays and Mondays.
> It should also be noted that the `userSettings.json` file contains a field called `DesiredResponseTime`, where you can specify the time the application will use as a reference. If the application is launched after this time, it will retrieve data for the current day; if launched earlier, it will retrieve data for the previous day.

## Technology Stack

- **Programming Language**: C# (.NET 9)
- **Database**: MariaDB
- **Hosting**: Linux
- **API Data Source**: [European Central Bank](https://data.ecb.europa.eu/help/api/overview)
- **Task Scheduler**: CRON

## Application Features

1. Daily (at 00:00 except Sundays and Mondays) retrieval of exchange rates via the ECB API.
2. Storing data in the MariaDB database.
3. Ensuring idempotency (no duplicate records for the same date).
4. Error handling and logging.
5. Administrator notifications in case of error.

## Database Schema

```sql
CREATE TABLE CurrencyRates (
    id INT AUTO_INCREMENT PRIMARY KEY,
    date DATE NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    exchange_rate DECIMAL(15,6) NOT NULL,
    UNIQUE KEY unique_date_currency (date, currency_code),
    INDEX idx_date (date),
    INDEX idx_currency_code (currency_code)
);
```

## Required NuGet Packages for Visual Studio 2022

```
Microsoft.EntityFramework
Microsoft.EntityFramework.Design
Microsoft.Extensions.Configuration
Microsoft.Extensions.Configuration.Json
MySql.EntityFrameworkCore
SeriLog
Serilog.Sinks.File
Microsoft.AspNetCore.OpenApi
```

## Installation and Setup

### Step 1: Setting up the Linux Virtual Machine

1. Choose a Linux distribution (e.g., Ubuntu 24.04.2) and create a virtual machine.
2. Enable Bridged Adapter in the virtual machine settings and select the correct Ethernet name.
3. Open port 3306 in the virtual machine firewall using the following commands:

```sh
sudo ufw enable
sudo ufw allow 3306
sudo ufw reload
```

### Step 2: Installing and Connecting to MariaDB

1. Download MariaDB for Windows. You can download it from the [official website](https://mariadb.org) and optionally install **HeidiSQL** for database management.
2. Open the virtual machine console and update packages:

```sh
sudo apt update && sudo apt upgrade
```

3. Install MariaDB on the virtual machine:

```sh
sudo apt install mariadb-server
```

4. Ensure MariaDB is running:

```sh
sudo systemctl status mariadb
```

5. To allow remote connections, edit `/etc/mysql/mariadb.conf.d/50-server.cnf`, find the `bind-address` setting, and change it to `0.0.0.0`. Restart MariaDB:

```sh
sudo systemctl restart mariadb
```

6. Create a database user with remote access (replace `admin` and `password` with your own values):

```sql
CREATE USER 'admin'@'%' IDENTIFIED BY 'password';
GRANT SELECT, INSERT, UPDATE, DELETE, INDEX ON CurrencyDB.* TO 'admin'@'%';
FLUSH PRIVILEGES;
```
> [!NOTE]
> If you want to create a user without a remote duo, then write `localhost` instead of the `%` sign

7. Create the database:

```sql
CREATE DATABASE CurrencyDB;
USE CurrencyDB;
```

8. Create the table:

```sql
CREATE TABLE Currencies (
    id INT AUTO_INCREMENT PRIMARY KEY,
    currency_code CHAR(3) NOT NULL UNIQUE,
    INDEX idx_currency_code (currency_code)
);

CREATE TABLE CurrencyRates (
    id INT AUTO_INCREMENT PRIMARY KEY,
    date DATE NOT NULL,
    currency_id INT NOT NULL,
    exchange_rate DECIMAL(15,6) NOT NULL,
    UNIQUE KEY unique_date_currency (date, currency_id),
    INDEX idx_date (date),
    INDEX idx_currency_id (currency_id),
    CONSTRAINT fk_currency_id FOREIGN KEY (currency_id) 
        REFERENCES Currencies(id)
        ON DELETE CASCADE ON UPDATE CASCADE
);
```

> [!TIP]
> If you installed HeidiSQL, create a new session, name it as desired, and enter the required connection details (IP, username, password, port, and database name). This makes database management easier but is not mandatory.

### Step 3: Configuring the Project Code
#### System Configuration
For the system to function correctly, please replace the default values in the `systemSettings.json` file with your own data.

1. Open the `systemSettings.json` file. An example of the file is shown below:
```json
{
  "SystemPreferences": {
    "Database": {
      "Name": "your_database_name",
      "Address": "your_ip_adress",
      "Port": 3306,
      "User": "your_username",
      "Password": "your_db_password"
    },
    "Smtp": {
      "Host": "your_smtp",
      "Port": 587,
      "Password": "your_application_password"
    }
  }
}
```
2. Make changes to the following fields:
- __Database__: Specify the parameters for your database (Name, Address, User, Password).
- __Smtp__: Specify the parameters for your SMTP server (Host, Port, Password).

Save the file after making the changes.


#### User Configuration
To customize the user preferences, modify the `UserPreferences` section in the `userSettings.json` file as follows:
```json
{
  "UserPreferences": {
    "DesiredResponseTime": "16:00", 
    "Sender": "your_email@example.com", 
    "Recipient": "recipient_email@example.com" 
  }
}
```
- DataRefreshInterval: Refresh time, which determines the selection of data for today or yesterday
- Sender: Enter the email address that will be used to send notifications.
- Recipient: Enter the recipient's email address (can be your own or another's).

> [!NOTE]
> 1. If you are not using Gmail, you can find the appropriate SMTP server [here](https://www.smtpsoftware.com/smtp-server-list/).
> 2. Gmail users must generate an [Application Password](https://support.google.com/accounts/answer/185833?hl=en) for authentication.
>
> After deploying the project, these files will be copied along with the launch files(.exe, .dll and etc.), allowing you to update all the data in the future.


### Step 4: Deployment and CRON Setup
#### Deploying the Project Using Visual Studio 2022

1. Open your project in Visual Studio 2022.
2. Click **Build → Publish Selection**.
3. Select **Folder** as the publication type and choose a destination, e.g., `bin\Release\net9.0\publish\`.
4. Click **Publish**.

#### Setting Up SSH Connection

1. Install SSH on the virtual machine:
```sh
sudo apt install openssh-server
```

2. Enable and start SSH:
```sh
sudo systemctl enable ssh
sudo systemctl start ssh
```

3. Verify SSH status:
```sh
sudo systemctl status ssh
```

4. Open port 22 for SSH:
```sh
sudo ufw allow 22
sudo ufw reload
```

5. Check available ports:
```sh
sudo ufw status
```

6. To test the SSH connection, find your Linux machine's IP using:
```sh
ip addr
```

7. On your Windows machine, run the following in PowerShell (replace `username` and `0.0.0.0` with your actual values):
```sh
ssh username@0.0.0.0
```

#### Transferring the Project to the Linux Virtual Machine
Use PowerShell on Windows to copy files to the Linux server:
```sh
scp -r bin/Release/net9.0/publish/ username@server_ip:/home/user/published_project
```

Replace `server_ip` with the Linux machine's actual IP address, and `user` to your user name on Linux machine.

#### Preparing the Linux Environment
##### Installing .NET Runtime

Run the following in the Linux console (replace `22.04` with your distribution version):
```sh
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x ./dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime dotnet
```

Verify installation:
```sh
dotnet --list-runtimes
```

[Official documentation](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install)

##### Configuring CRON for Daily Execution
1. Open CRON editor: `crontab -e`
2. Select `/bin/nano` editor.
3. Add the following line (replace `user` with your Linux username):

```sh
0 0 * * 2-6 dotnet /home/user/published_project/CurrencyRateFetcher.dll
```

> [!NOTE]
> It should be noted that in my project there are also keys that can be used to run the application.
> Here is a list of these keys:
> 
> - `--help` :        Show all keys
> - `today`  :        Get data for the current day
> - `yesterday` :     Get data for the previous day
>  


# Additional Technology
## Overview
In addition to the main application, a complementary project named **CurrencyApi** has been developed. The **CurrencyApi** is an independent project developed to extend the capabilities of the main application. This API provides program access to the exchange rate data stored in the MariaDB database __locally__ on system where it was started. 

## Configuration
### Preliminary preparation
First, you have to check if your machine is ready for the API. To do this, go to the **Linux virtual machine** console and enter the following command:
```sh
sudo apt install net-tools
```

After this (if you changed the port, don't forget to do so here as well):
```sh
netstat - tuln | grep 3306
```

If the console displays something like this, then everything was successful:
```sh
tcp   0   0   0.0.0.0:3306    0.0.0.0:*      LISTEN
```

If you encounter any errors, check your ports. You may also try entering the following commands (just replace `your_port`):
```sh
sudo ufw allow your_port/tcp
sudo ufw reload
```

### Integration of an additional project with the main one
Create a separate folder on your computer, deploy two projects, `CurrencyRateFetcher` and `CurrencyApi`, into this folder. Just like last time, send these files to the Linux machine.

Now you need to install the `Microsoft.AspNetCore.App` framework. To do this, enter the following commands:
```
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-9.0
```

> [!IMPORTANT]
> If your virtual machine **is not Ubuntu 24.04**, go to the official [Microsoft documentation](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime?cid=getdotnetcore&runtime=aspnetcore&os=linux&arch=x64) and check which command is needed specifically for your system.

After that, you can navigate to the project folder and run the project using the command dotnet `CurrencyApi.dll`

You can view the content of the API by going to any browser and entering the link that will be displayed in the console with the postscript `/api/currencyRates`, for example, like this:
```
https://localhost:5005/api/currencyRates
```

