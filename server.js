var express = require('express');

var port = process.env.PORT || 1337;
var app = express();

app.use(express.static('public'));

app.set('views', './src/views');
app.set('view engine', 'pug');

app.get("/", function (req, res) {
    res.render('TestPage', { valList: [{ Text: 'Value 1', Text2: ' Subtext' }, { Text: 'Value 2', Text2: ' Subtext' }] });
});

app.listen(port, function (err) {
    console.log('Server is running on port ' + port);
});

