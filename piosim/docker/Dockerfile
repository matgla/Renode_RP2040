FROM archlinux:base-20241124.0.282387

RUN pacman --noconfirm -Syu
RUN pacman --noconfirm -Sy gcc clang cmake
RUN pacman --noconfirm -S git base-devel
RUN mkdir -p /tmp/yay-build
RUN useradd -m -G wheel builder && passwd -d builder && chown -R builder:builder /tmp/yay-build

RUN echo "builder ALL=(ALL) NOPASSWD: ALL" >> /etc/sudoers
RUN su - builder -c "git clone https://aur.archlinux.org/yay.git /tmp/yay-build/yay"
RUN su - builder -c "cd /tmp/yay-build/yay && makepkg -si --noconfirm"
RUN rm -rf /tmp/yay-build
RUN su - builder -c "yay --noconfirm -Syu && yay --noconfirm -S mingw-w64-gcc mingw-w64-cmake"
