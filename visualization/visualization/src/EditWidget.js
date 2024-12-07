import { Typography, Box, Stack, Button } from '@mui/material';
import { CirclePicker } from 'react-color';
import { forwardRef, useImperativeHandle, useRef, useState } from 'react';
import './App.css';

const EditWidget = forwardRef(({ }, ref) => {
  const colorChangeCallback = useRef();
  const [componentName, setComponentName] = useState(null);

  useImperativeHandle(ref, () => ({
    registerForColorChange(onColorChange, name) {
      colorChangeCallback.current = onColorChange;
      setComponentName(name);
    }
  }));

  const notifyColorChange = (color) => {
    if (colorChangeCallback.current) {
      colorChangeCallback.current(color.hex);
    }
  }
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
          }}> Edit </Typography>
          <Typography align="center" variant="h5" gutterBottom sx={{
            color: 'black'
          }}> {componentName} </Typography>
          {componentName && (<Box sx={{ display: "flex", justifyContent: "center" }}><CirclePicker className="colorPicker" id="picker" width="100%" styles={{ default: { circlePicker: { justifyContent: "center" } } }} onChangeComplete={notifyColorChange} /></Box>)}
          {componentName && (<Button variant="contained" onClick={() => setComponentName(null)}>Close</Button>)}
        </Stack>
      </Box>
    </div >
  )
});

export default EditWidget;
