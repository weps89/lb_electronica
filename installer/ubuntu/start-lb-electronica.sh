#!/usr/bin/env bash
set -euo pipefail
sudo systemctl start lb-electronica
sudo systemctl status lb-electronica --no-pager
