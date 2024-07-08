import os
import psutil
import subprocess
import sys
import yaml

DEFAULT_FORMAT = "opus"
FORMAT = os.getenv("FORMAT", DEFAULT_FORMAT)

OPTIONS = os.getenv("OPTIONS", str())

NUM_THREADS = psutil.cpu_count(logical=True)

def load_yaml(file_path):
    """
    Loads the contents of a YAML file.

    :param file_path: Path of the YAML file.
    :return: Content of the YAML file as a dictionary.
    """
    with open(file_path, 'r', encoding='utf-8') as file:
        return yaml.safe_load(file)

def download_yaml(yaml_content, type):
    print(f"Processing {type}...", flush=True)
    entries = extract_info_yaml(yaml_content, type)
    for entry in entries:
        download_music(entry)
    print(f"{type} processed", flush=True)

def extract_info_yaml(yaml_content, type):
    """
    Extract information from YAML content.

    :param yaml_content: Content of the YAML file as a dictionary.
    :return: List of dictionaries with information about the yaml entries.
    """
    entries = yaml_content.get(type, [])
    return entries

def download_music(entry):
    """
    Run the spotdl command to download music and embed Spotify metadata.

    :param entry: Entry from Spotify to download.
    """
    name = entry['name']
    url = entry['url']
    refresh = entry.get('refresh', True)

    print(f"Downloading: {name}")

    os.chdir(f'/music')

    print(f"Creating directory: {name}...", flush=True)
    if not os.path.exists(name):
        os.makedirs(name)
        print("Directory created", flush=True)
    else:
        print("Directory already exists", flush=True)
        if not refresh and os.listdir(name) != []:
            print("Directory contains files and property 'refresh' is set to False -> Skipping...", flush=True)
            return

    os.chdir(f'/music/{name}')

    command = ['/usr/local/bin/spotdl', 'download', url, '--format', FORMAT, '--threads', str(NUM_THREADS)]
    for arg in OPTIONS.split():
        command.append(arg)

    with subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True) as process:
        for line in process.stdout:
            print(line, flush=True)
        process.wait()
        if process.returncode == 0:
            print(f"spotdl exited successfully for query: {url}", flush=True)
        else:
            print(f"spotdl exited with errors for query: {url}", flush=True)

    os.chdir(f'/music')

def main(file_path):
    """
    :param file_path: YAML file path.
    """
    print(f"I will download music as {FORMAT} files with {NUM_THREADS} threads", flush=True)
    
    print("Loading tracking info...", flush=True)
    yaml_content = load_yaml(file_path)
    print("Tracking info loaded", flush=True)

    download_yaml(yaml_content, 'artists')

    download_yaml(yaml_content,'playlists')

if __name__ == "__main__":

    print("Program started", flush=True)

    if len(sys.argv) != 2:
        print("Use: python script.py <tracking.yaml>", flush=True)
    else:
        file_path = sys.argv[1]
        main(file_path)

    print("Program finished", flush=True)
