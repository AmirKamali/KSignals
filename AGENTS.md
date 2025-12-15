Whenever user requests a new feature on the web page, follow the guidelines below:
1- If feature needs API, create the API on backend project and use any DTO object in the shared/KSignals.DTO
2- Configure the frontend and add UI in C# razor page, add logics in the postback to handle input, validate and send to backend and display results to the user.
3- Make sure project that has been changed is able to compile after new changes