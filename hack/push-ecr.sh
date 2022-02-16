#! /bin/sh

set -euo pipefail
SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

log() { echo "$1" >&2; }

TAG="${TAG:?TAG env variable must be specified}"

while IFS= read -d $'\0' -r dir; do
    # build image
    WHAT="$(basename "${dir}")"
    
    docker tag $WHAT:$TAG 074157727657.dkr.ecr.us-east-2.amazonaws.com/$WHAT:$TAG
    docker push 074157727657.dkr.ecr.us-east-2.amazonaws.com/$WHAT:$TAG
done
