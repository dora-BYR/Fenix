#!/bin/bash

export cwdpath=$(pwd)
export curpath=${cwdpath}/..

export APP_CONF=${curpath}/conf/app.json

export BIN_PATH=${cwdpath}/netcoreapp3.1

echo ${BIN_PATH}/Server.App.dll

nohup dotnet ${BIN_PATH}/Server.App.dll --Config=${APP_CONF} --AppName="Login.App" > /dev/null 2>&1 &
nohup dotnet ${BIN_PATH}/Server.App.dll --Config=${APP_CONF} --AppName="Match.App" > /dev/null 2>&1 &
nohup dotnet ${BIN_PATH}/Server.App.dll --Config=${APP_CONF} --AppName="Master.App" > /dev/null 2>&1 &
nohup dotnet ${BIN_PATH}/Server.App.dll --Config=${APP_CONF} --AppName="Zone.App" > /dev/null 2>&1 &

