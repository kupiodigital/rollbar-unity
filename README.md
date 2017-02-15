# rollbar-unity
A rollbar error logging component for Unity projects.

Simply copy the file `Assets/Plugins/Kupio/RollbarMonitoring/RollbarNotifier.cs` into the equivalent location in your project.

## Setup

Add the "Rollbar Notifier" component to a game object:

![Add component](https://raw.githubusercontent.com/kupiodigital/rollbar-unity/master/img/addcomponent.png)

Add in your config settings using the inspector:

![Inspector settings](https://raw.githubusercontent.com/kupiodigital/rollbar-unity/master/img/inspector.png)

The client token should be from your rollbar account, and the project you wish to log messages to.

The production and development environment values are the names of the profiles you with to use for production and development logging. If your app is running in the editor, then it's a development environment, otherwise it's production.

The 'Production' and 'Development' checkboxes enable logging in each of those environments. Normally, you would only enable it for production.

The 'Debug log for messages' and 'Debug log for exceptions' options will echo any rollbar messages to the unity console.
