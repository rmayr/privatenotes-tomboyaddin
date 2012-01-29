#!/bin/bash
## copies newly built libraries from the infinote-library. 
## if the infinote library has not been built successfully for some reason
## there is a fallback which downloads some precompiled versions
##
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
LIBSDIR="$DIR/../../../../../InfinoteLib/Infinote/Infinote/bin/Debug"

LIBSDIR="$( readlink -m $LIBSDIR )"

if ([ -d $LIBSDIR/ ] && [ -e $LIBSDIR/Infinote.dll ]); then
	echo "file exists :)"
else
	mkdir -p $LIBSDIR
	echo "Infinote.dll does not exist in InfinoteLib/Infinote/Infinote/bin/Debug, downloading precompiled version"
	if [ -e $LIBSDIR/precompiled.zip ]; then
		echo "file already here, not downloading again..."
	else
			wget -O $LIBSDIR/precompiled.zip http://dl.dropbox.com/u/1526874/PrivateNotes/precompiledlibs.zip
		if [ "$?" -ne 0 ]     # test if we finished
		then
			echo "Error while downloading file... exiting"
		    exit
		fi
	fi
	unzip -o $LIBSDIR/precompiled.zip -d $LIBSDIR
fi

echo "copying new files..."

cp -f $LIBSDIR/*.dll $DIR/

echo "done"