from datetime import datetime
def on_config(config, **kwargs):
    config.copyright = f"Copyright © 2024 - {datetime.now().year} Pablo Meca – <a href=\"#__consent\">Change cookie settings</a>"