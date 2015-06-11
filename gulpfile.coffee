gulp = require 'gulp'
args = require('yargs').argv
dotnet = require 'gulp-dotnet-utils'

pkg = require './package.json'

configuration = if args.debug then 'Debug' else 'Release'

gulp.task 'default', ['check-await', 'test']

gulp.task 'build', ['restore'], ->
  dotnet.build configuration, ['Clean', 'Build']

gulp.task 'clean', -> dotnet.build configuration, ['Clean']

gulp.task 'test', -> dotnet.test [
  "tests/Caching.Tests/bin/#{configuration}/Vtex.Caching.Tests.dll"
  ], if args.redis then {} else exclude: ['Redis']

gulp.task 'restore', -> dotnet.exec 'nuget restore'

gulp.task 'check-await', ->
  dotnet.checkAwait 'src/**/*.cs'

gulp.task 'pack', ->
  specs = ['src/Caching/Caching.nuspec']
  dotnet.nuget.pack specs, './', pkg.version

gulp.task 'bump', ->
  dotnet.bump pkg.version
