include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-define:NOSHARES	\
	-target:library

ASSEMBLIES = 						\
	$(LINK_TOMBOY_EXE)				\
	$(GTKSHARP_LIBS) 				\
	$(LINK_MONO_ADDINS)				\
	-r:$(top_builddir)/Tomboy/Addins/PrivateNotesAddin/libs/XmppOtrLibrary.dll	\
	-r:$(top_builddir)/Tomboy/Addins/PrivateNotesAddin/libs/Infinote.dll		\
	-r:$(top_builddir)/Tomboy/Addins/PrivateNotesAddin/libs/zxing.dll		\
	-r:Mono.Posix

TARGET = $(top_builddir)/bin/addins/PrivateNotes.dll
CSFILES = \
	$(srcdir)/AddinPreferences.cs									\
	$(srcdir)/AddinPreferencesFactory.cs					\
	$(srcdir)/CryptoLayer.cs											\
	$(srcdir)/EncryptedWebdavSyncServer.cs				\
	$(srcdir)/EncryptedWebdavSyncServiceAddin.cs	\
	$(srcdir)/PrivateNotesWebDavShareProvider.cs	\
	$(srcdir)/Program.cs													\
	$(srcdir)/SecureSharingFactory.cs							\
	$(srcdir)/ShareNoteAddin.cs										\
	$(srcdir)/ShareProvider.cs										\
	$(srcdir)/ShareSync.cs												\
	$(srcdir)/SharingAppAddin.cs									\
	$(srcdir)/Statistics.cs												\
	$(srcdir)/Util.cs															\
	$(srcdir)/WebDAVClient.cs											\
	$(srcdir)/WebDAVInterface.cs									\
	$(srcdir)/cryptScheme/*.cs										\
	$(srcdir)/gui/*.cs														\
	$(srcdir)/Hyena.Json/*.cs											\
	$(srcdir)/Infinote/*.cs											\
	$(srcdir)/local/*.cs
RESOURCES = \
	-resource:$(srcdir)/PrivateNotes.addin.xml

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb	\
	$(top_builddir)/Tomboy/Addins/PrivateNotesAddin/libs/*.dll

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/PrivateNotes.addin.xml	\
	$(srcdir)/libs/*.dll


CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)
