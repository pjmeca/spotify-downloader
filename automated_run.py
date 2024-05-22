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

def extract_artists(yaml_content):
    """
    Extract artists information from YAML content.

    :param yaml_content: Content of the YAML file as a dictionary.
    :return: List of dictionaries with information about the artists.
    """
    artists = yaml_content.get('artists', [])
    return artists

def download_artist_music(artist):
    """
    Run the spotdl command to download music from an artist.

    :param artist: Artist from Spotify to download.
    """
    name = artist['name']
    url = artist['url']

    print(f"Downloading artist: {name}")

    print(f"Creating directory: {name}...", flush=True)
    if not os.path.exists(name):
        os.makedirs(name)
        print("Directory created", flush=True)
    else:
        print("Directory already exists", flush=True)

    os.chdir(f'/app/{name}')

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

def main(file_path):
    """
    :param file_path: YAML file path.
    """
    print(f"I will download music as {FORMAT} files with {NUM_THREADS} threads", flush=True)
    
    yaml_content = load_yaml(file_path)
    artists = extract_artists(yaml_content)
    for artist in artists:
        download_artist_music(artist)

if __name__ == "__main__":

    print("Program started", flush=True)

    if len(sys.argv) != 2:
        print("Use: python script.py <tracking.yaml>", flush=True)
    else:
        file_path = sys.argv[1]
        main(file_path)

    print("Program finished", flush=True)
