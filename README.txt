This app is an LTI tool for checking if a certain quiz has been passed, and if so generating a PDF certificate and emailing it to the student. 
It includes a form (see '/generate-config') for generating the XML config needed, which will differ for each use of the tool. 

The data needed for the config is 
    1) ID of the quiz to check for the certificate, and 
    2) Score requirement to consider the quiz "passed". 
These parameters are inserted into the XML config and subsequently passed back to the app when the LTI tool is launched from Canvas.

The tool should be added at the course level so students can use it to generate their own certificates. 
When a student launches the tool, the app receives a post request from Canvas with various parameters. 
It pulls submissions for the specified quiz from the API, and checks if any exist that have a passing score and also belong to the specified user. 
If they do exist, a Resque job is started to generate the certificate. 
The API cache for checking submissions is limited to 5 minutes; students usually get their certificates shortly after completing the quiz, so the submission data needs to be fairly up-to-date.

The worker uses an executable on the server called wkhtmltopdf (http://wkhtmltopdf.org/) to render an html view of the 
certificate into a PDF containing the users name, course, and timestamp. 
It requires xvfb (https://en.wikipedia.org/wiki/Xvfb) in order to render properly on a headless server.