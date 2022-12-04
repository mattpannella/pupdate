<?php
require_once('updater.php');
$token = $argv[1];

$updater = new Updater($token);
$updater->checkForUpdates();
