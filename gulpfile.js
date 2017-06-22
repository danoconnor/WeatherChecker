var gulp = require('gulp');

gulp.task('inject', function () {
    var inject = require('gulp-inject');
    var wiredep = require('wiredep').stream;
    var wiredepOptions = {
        bowerJson: require('./bower.json'),
        directory: './public/lib',
        ignorePath: '../../public'
    };

    var injectSrc = gulp.src(['./public/css/*.css', './public/js/*.js'], { read: false });
    var injectOptions = {
        ignorePath: 'public'
    };

    return gulp.src('./src/views/*.html')
        .pipe(wiredep(wiredepOptions))
        .pipe(inject(injectSrc, injectOptions))
        .pipe(gulp.dest('./src/views'));
});

gulp.task('serve', ['inject'], function () {
    var nodemon = require('gulp-nodemon');
    var serveOptions = {
        script: 'server.js',
        delayTime: 1,
        env: {
            'PORT': 1337
        },
        watch: ['*.js', 'src/**/*.js']
    };

    return nodemon(serveOptions)
        .on('restart', function (ev) {
            console.log('restarting...');
        });
});