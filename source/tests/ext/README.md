# Ext reader fixtures

create-fixtures.py generates minimal ext2, MBR and GPT images with 512/4096-byte sectors, Unicode names, case collisions, a symbolic link, a device inode and a sparse file larger than 4 GiB. These contain generated demonstration data only.

data.ext4.dat.gz and data.ext4.links.gz are upstream DiscUtils test fixtures from commit 414c7f7b2d82c42b07bc6fdfc18a763331334697 (Tests/LibraryTests/Ext); see DISCUTILS-LICENSE.txt (MIT).

Run the development build with --ext-reader-test <fresh-output-directory>. Tests never format or eject a real device. Physical USB access still needs validation on supported hardware.
