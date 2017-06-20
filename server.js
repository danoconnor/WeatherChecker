var express = require('express');

var port = process.env.PORT || 1337;
var app = express();

app.use(express.static('public'));
app.use(express.static('src/views'));

app.get("/", function (req, res) {
    res.send('Hello  world');
});

app.listen(port, function (err) {
    console.log('Server is running on port ' + port);
});

