import { CloudUpload, Save } from '@mui/icons-material';

import { Typography, Button, Box, Stack } from '@mui/material';

import { styled } from '@mui/material/styles';

const VisuallyHiddenInput = styled('input')({
  clip: 'rect(0 0 0 0)',
  clipPath: 'inset(50%)',
  height: 1,
  overflow: 'hidden',
  position: 'absolute',
  bottom: 0,
  left: 0,
  whiteSpace: 'nowrap',
  width: 1,
});


const LayoutWidget = ({ openLayout, onSaveLayout }) => {
  return (
    <div>
      <Box sx={{
        padding: '10px',
        borderRadius: '8px',
        gap: '10px',
        boxShadow: 3,
      }}>
        <Stack
          spacing={{ xs: 1, sm: 2 }}
          direction="row"
          useFlexGap
          sx={{
            flexWrap: 'wrap',
            justifyContent: 'center',
            alignContenet: 'center'
          }}>
          <Typography align="center" variant="h5" gutterBottom sx={{
            color: 'black',
            width: '100%'
          }}> Layout </Typography>
          <Button
            variant="contained"
            onClick={onSaveLayout}
            startIcon={<Save />}
          >Save</Button>
          <Button
            component="label"
            variant="contained"
            tabIndex={-1}
            startIcon={<CloudUpload />}
          >
            Load
            <VisuallyHiddenInput
              type="file"
              onChange={openLayout}
              multiple
            />
          </Button>
        </Stack>
      </Box>
    </div >

  )
}

export default LayoutWidget;
