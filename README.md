![AuthJanitor Logo](https://github.com/microsoft/AuthJanitor/blob/master/docs/assets/img/AJLogoDark.png?raw=true)

![.NET Core](https://github.com/microsoft/AuthJanitor/workflows/.NET%20Core/badge.svg?branch=master)

Manage the lifecycle of your application secrets in Azure with ease. Migrate to more secure, auditable operations standards on your own terms. AuthJanitor supports varying levels of application secret security, based on your organization's security requirements.

*Disclaimer:* Using AuthJanitor does not guarantee the security of your application. There is no substitute for a proper security review from a reputable cybersecurity and/or auditing partner.

:red_circle: **This system has not been thoroughly tested yet! Please use at your own risk!** :red_circle:

### Installation (Azure)
#### Create the AuthJanitor application
Follow the instructions at https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app to register a new application in the Active Directory tenant where you expect to run AuthJanitor. You may use any nomenclature you prefer; make note of the Client ID and generate a Client Secret. These will be necessary for the application to run properly. The application must have the following permissions:

* Azure AD - Sign in and Read User Profile
* Azure Service Management API - Access as organization users (_ADMIN CONSENT REQUIRED_)

(Optional for User Management to work)
* Graph - Read Applications
* Graph - Manage App Permission Grants and Role Assignments
* Graph - Read All users' basic profiles

#### Deploy Infrastructure
Once your application is created, you can quickly deploy AuthJanitor's infrastructure to your Azure subscription by clicking the button below:

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2FAuthJanitor%2Fmaster%2Fauthjanitor.azuredeploy.json)

You will need your Client ID and Client Secret from the application created above.

#### Deploy Code
At the moment, there is no quick way to deploy the AuthJanitor application (expect this soon). Currently, you must build/publish the code manually:
1) Build and publish the `AuthJanitor.AspNet.AdminUi` project to a file folder.
2) Upload the content of the `publish/wwwroot` directory to the $web share of the AuthJanitor storage account created by the ARM template
3) Enable the "Static Website" feature of the AuthJanitor storage account from the Azure Portal. (Currently there is no way to do this via ARM.)
4) Build and publish the `AuthJanitor.Functions.AdminApi` project to the Functions app created in the deployment.
5) Navigate to the URL for the new Functions app and launch "Configuration Wizard" from the main menu, if it does not appear automatically.

### :unlock: Learn more about how AuthJanitor can improve the security around your application secrets [here](https://github.com/microsoft/AuthJanitor/wiki/Authentication-Authorization-Concepts).

### Secret Rotation Process
![Secret Rotation Process](../master/docs/assets/img/SecretRotationWorkflow.png?raw=true)

## Glossary of Terms
#### Provider
A module which implements some functionality, either to handle an **Application Lifecycle** or rekey a **Rekeyable Object**.

##### Provider Configuration
Configuration which the **Provider** consumes in order to access the **Application Lifecycle** or **Rekeyable Object**.

#### Application Lifecycle Provider
A **Provider** with the logic necessary to handle the lifecycle of application which consumes some information (key, secret, environment variable, connection string) to access a **Rekeyable Object**.

#### Rekeyable Object Provider
A **Provider** with the logic necessary to rekey a service or object. This might be a database, storage, or an encryption key.

#### Resource
A GUID-identified model which joins a **Provider** with its **Provider Configuration**. Resources also have a display name and description. A Resource can either wrap an **Application Lifecycle** _or_ a **Rekeyable Object** provider.

#### Managed Secret
A GUID-identified model which joins one or more **Resources** which make up one or more rekeyable objects and their corresponding application lifecycles. When using multiple rekeyable objects and/or lifecycles, a **User Hint** must be specified. When the rekeying is performed, the **User Hints** are matched between rekeyable objects and application lifecycles to identify where different secrets should be persisted. Managed Secrets also have a display name and description, as well as metadata on the validity period and rotation history.

#### Rekeying Task
A GUID-identified model to a **Managed Secret** which needs to be rekeyed. A Rekeying Task has a queued date and an expiry date; the
expiry date refers to the point in time where the key is rendered invalid. A Rekeying Task must be approved by an administrator or will be executed automatically by the AuthJanitor Agent.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
