# Simple AWS Serverless (SAM) Blog

A simple AWS Serverless (SAM) blog created based on [Amazon DynamoDB Blog API](https://github.com/aws/aws-lambda-dotnet) using [.NETCore 2.0](https://www.microsoft.com/net/download/windows)

# To Get Started

> **NOTE** This template will generate an AWS API Gateway and its lambda methods. However, it does not set the lambda authorizer, so anyone with your URL would be able to create a post/user

> **NOTE-2** Currently, the user's password will be stored as `email` + `password` + `secret`. So, don't use it as production or implement a ~~better~~ `crypt`. (I'll do that later on.)

* Install [.NETCore 2.0](https://www.microsoft.com/net/download/windows)
* Install [AWS SDK for .NET](https://aws.amazon.com/sdk-for-net/)

And, change `serverless.template` parameters

```yaml
Parameters:
  BlogTableName:
    Default: # blog table name
  UserTableName:
    Default: # user table name
  Salt:
    Default: # unique salt phrase
  Issuer:
    Default: # some issuer string
  Audience:
    Default: # some audience string
  Secret:
    Default: # some secret to create JWT
```

To publish, execute

```
dotnet lambda deploy-serverless -sn <name of the new CloudFormation stack> -sb <An S3 bucket> --region <any-aws-region>
```

Once published, you'are ~~all set~~ almost done.

* Go to your **AWS Api Gateway Console** > _[api name]_ > **Authorizers**,
* Create a new \*_Authorizer_

  * **Name**: _Authorizer's Name_
  * **Lambda Function**: choose _[AuthLambda]_ function
  * **Lambda Invoke Role**: _[leave it blank]_
  * **Lambda Event Payload**: _Token_
  * **Token Source**: _Auth_ `# The Blog uses this`

* Go to **Resources** :

  > and for each of these: **/blogs** [`DELETE`, `POST`], **/me** [`GET`] **/users** [`DELETE`, `POST`, `GET`, `POST`]

  * Open _[method]_'s **Method Request**
  * Choose _[AuthorizerName]_ as your **Authorization**
  * Choose `Validate body, query string parameters, and headers` for **Request Validator**
  * In **HTTP Request Headers**, input **Auth** as a new Header and, set it as **Required**

* Once the you finish the **_requests setup_**, go to:

  > for each resource **[/...]**

  * **Actions** > **Deploy API** > _[choose a stage name]_

* Now you are done!

## Who came first, the :hatched_chick: or the :egg: ?

Before you set an `Authorizer` for `/users` [`POST`], it is recommended to create an `Admin` user.

```HTTP
POST /prod/users HTTP/1.1
Host: <your api url>
Content-Type: application/json
Cache-Control: no-cache
Postman-Token: 25d0a1c0-7476-c8f8-5671-a4b5a9140eb4
{
  "name": "Felipe Correa",
  "email": "felipe.correa (at) (google's Mail).com",
  "password": "my secure passwrod",
  "isAdmin": true
}
```

#

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can use the following command lines to deploy your application from the command line (these examples assume the project name is _EmptyServerless_):

Restore dependencies

```
    cd "BlogApi"
    dotnet restore
```

Deploy application

```
    cd "BlogApi/src/BlogApi"
    dotnet lambda deploy-serverless
```
