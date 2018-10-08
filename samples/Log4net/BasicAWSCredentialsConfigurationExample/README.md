#Introduction
This project shows you how to configure AWS CloudWatchLog with AWS basic credentials.

It is useful for a project that is not hosted on an AWS environment, e.g. 
other dedicated servers, on-premise server.

#How to run a project
- Open a project with Visual Studio
- In the Solution Explorer window, right click on the project name node. 
  Select `Set as Startup Project`
- Go to the project properties window
- Select the Debug tab and then add your AWS API key and secret key to Command line arguments text box as 
  the following pattern: `YouAwsApiKey YourAwsSecretKey`
- Run a project with debugging by pressing `F5`
- Log in to your AWS console, go to Cloud watch log. You should see a new log message in
`Log4net.BasicAWSCredentialsConfigurationExample` group.