LOG2LINES_BASE = $(TOOLS_BASE)$(SEP)rsym
LOG2LINES_BASE_ = $(LOG2LINES_BASE)$(SEP)

LOG2LINES_INT = $(INTERMEDIATE_)$(LOG2LINES_BASE)
LOG2LINES_INT_ = $(LOG2LINES_INT)$(SEP)
LOG2LINES_OUT = $(OUTPUT_)$(LOG2LINES_BASE)
LOG2LINES_OUT_ = $(LOG2LINES_OUT)$(SEP)

LOG2LINES_TARGET = \
	$(LOG2LINES_OUT_)log2lines$(EXEPOSTFIX)

LOG2LINES_SOURCES = \
	$(LOG2LINES_BASE_)log2lines.c \
	$(LOG2LINES_BASE_)rsym_common.c

LOG2LINES_OBJECTS = \
	$(addprefix $(INTERMEDIATE_), $(LOG2LINES_SOURCES:.c=.o))

LOG2LINES_HOST_CFLAGS = $(TOOLS_CFLAGS)

LOG2LINES_HOST_LFLAGS = $(TOOLS_LFLAGS)

.PHONY: log2lines
log2lines: $(LOG2LINES_TARGET)

$(LOG2LINES_TARGET): $(LOG2LINES_OBJECTS) | $(LOG2LINES_OUT)
	$(ECHO_HOSTLD)
	${host_gcc} $(LOG2LINES_OBJECTS) $(LOG2LINES_HOST_LFLAGS) -o $@

$(LOG2LINES_INT_)log2lines.o: $(LOG2LINES_BASE_)log2lines.c | $(LOG2LINES_INT)
	$(ECHO_HOSTCC)
	${host_gcc} $(LOG2LINES_HOST_CFLAGS) -c $< -o $@

#$(LOG2LINES_INT_)rsym_common.o: $(LOG2LINES_BASE_)rsym_common.c | $(LOG2LINES_INT)
#	$(ECHO_HOSTCC)
#	${host_gcc} $(LOG2LINES_HOST_CFLAGS) -c $< -o $@

.PHONY: log2lines_clean
log2lines_clean:
	-@$(rm) $(LOG2LINES_TARGET) $(LOG2LINES_OBJECTS) 2>$(NUL)
clean: log2lines_clean
