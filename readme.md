IFS1
==

Yet another implementation of IFS1.

IFS1 is the file system for [Ihsoh/isystemx86](https://github.com/Ihsoh/isystemx86).

Comments are written in Chinese.

Dependencies
==

1. [dotNet Framework 4.0](http://www.microsoft.com/en-us/download/details.aspx?id=17718)

1. [Microsoft Visual C++ 2013 Runtime](http://www.microsoft.com/en-us/download/details.aspx?id=40784)

IFS1Control Usage
==

	IFS1Control <command> [<arguments>]

Commands:

	mount, m -dfrs		Mount IFS1

	mkfs, makefs -dfl	Make file/device IFS1

Arguments:

	-c, --check								Check when mount

	-f, --file filename; -d, --dev [A-Z]:\	File name or device name

	-m, --mountpoint [A-Z]					Mount point

	-l, --length length(B/K/M/G/T)			Length for mkfs

	-r, --readonly							Readonly Mount

	-p, --repair							Repair if FS check failed



Test
==

1. Install [DokanY](https://github.com/Maxhy/dokany) driver. You can download the binaries from [here](http://files.twd2.net/dokany_binaries/), and the document is [here](http://files.twd2.net/dokany_binaries/readme.html).

1. Run ```IFS1Control mkfs -f test -l 1G``` and you will find a file named test generated.

1. Run ```IFS1Control mount -cpf test -m S```

1. Open S:\ from explorer and do whatever you want.

Test - Memory Disk
==

1. Install DokanY driver.

1. Run ```IFS1Control mount -t -l 100M -m S```

1. Open S:\ from explorer and do whatever you want.