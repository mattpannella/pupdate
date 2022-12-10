<?php
class Updater
{
    private const END_POINT = "https://api.github.com/repos/%s/%s/contents/%s";
    private const CORES_FILE = "pocket_updater_cores.json";
    private const REGEX = "/%s_([a-zA-Z0-9]*)\.zip/";
    private $token;
    private $cores = [];

    public function __construct($token)
    {
        if(!isset($token)) {
            die("ain't happenin, chief");
        }
        $this->token = $token;
        $this->readCurrentJSON();
    }

    private function getEndPoint($owner, $repo, $path)
    {
        return sprintf(self::END_POINT, $owner, $repo, $path);
    }

    private function fetchFiles($owner, $repo, $path = "")
    {
        $endpoint = $this->getEndPoint($owner, $repo, $path);
        $ch = curl_init($endpoint);
        $headers = [
            'User-Agent: Pocker Updater Utility',
            'Accept: application/vnd.github+json',
            "Authorization: Bearer {$this->token}"
        ];
        curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
        curl_setopt($ch, CURLOPT_TIMEOUT, 30);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, TRUE);
        $data = curl_exec($ch);
        curl_close($ch);

        return json_decode($data);
    }

    private function readCurrentJSON()
    {
        $data = file_get_contents(self::CORES_FILE);
        $cores = json_decode($data);
        $this->cores = $cores;
    }

    public function checkForUpdates()
    {
        $flag = false;
        foreach($this->cores as $i => $core) {
            if(!$core->mono) {
                continue;
            }

            $files = $this->fetchFiles($core->repository->owner, $core->repository->name, $core->release_path);
            if($core->version_type == "hash") {
                $multifile = false;
            } else {
                $multifile = true;
            }
            $new = $this->checkFiles($files, $core->identifier, $core->release->tag_name, $multifile);
            if($new) {
                $flag = true;
                echo "we found an update to {$core->identifier} {$new}";
                $this->cores[$i]->release->tag_name = $new;
                $this->cores[$i]->release->version = $new;
            }
        }

        if($flag) {
            $this->writeCoresFile();
        }
    }

    private function writeCoresFile()
    {
        $data = json_encode($this->cores, JSON_PRETTY_PRINT);
        file_put_contents(self::CORES_FILE, $data);
    }

    private function checkFiles($files, $basename, $current, $multifile)
    {
        $found = false;
        foreach($files as $file) {
            $name = $file->name;
            $regex = sprintf(self::REGEX, preg_quote($basename));
            if(preg_match_all($regex, $name, $matches)) {
                $version = $matches[1][0];
                if($multifile && $version > $current) {
                    $found = $version;
                } else if (!$multifile && $version != $current) {
                    $found = $version;
                }
            }
        }
        return $found;
    }
}
